namespace OrganizeMedia.Tests;

using OrganizeMedia.Framework;
using System.IO;

public class WorkflowObserverTests
{
    [Fact]
    public void RunToCompletion_NotifiesObserverForWorkflowAndTaskTransitions()
    {
        Workflow workflow = WorkflowTestSupport.CreateWorkflow(
            workflowId: "ObservedWorkflow",
            taskIds: ["A"],
            dependencies: Array.Empty<TaskDependencySpecification>());

        RecordingWorkflowObserver observer = new RecordingWorkflowObserver();
        RecordingFakeTaskExecutor taskExecutor = new RecordingFakeTaskExecutor(
            new Dictionary<string, IReadOnlyList<TaskExecutionResult>>
            {
                ["A"] = [TaskExecutionResult.Succeeded(new TextExecutionOutput("done"))]
            });

        workflow.RunToCompletion(taskExecutor, observer);

        Assert.Equal(
            [ExecutionPhase.ReadyToRun, ExecutionPhase.Queued, ExecutionPhase.Running, ExecutionPhase.Finished],
            observer.WorkflowTransitions.Select(transition => transition.CurrentStatus.ExecutionPhase));
        Assert.Equal(
            [ExecutionPhase.ReadyToRun, ExecutionPhase.Queued, ExecutionPhase.Running, ExecutionPhase.Finished],
            observer.TaskTransitions.Select(transition => transition.CurrentStatus.ExecutionPhase));
        Assert.All(
            observer.TaskTransitions,
            transition => Assert.Equal(new TaskId("A"), transition.TaskId));
        Assert.Equal(ExecutionOutcome.Pending, observer.WorkflowTransitions[0].PreviousStatus.ExecutionOutcome);
        Assert.Equal(ExecutionOutcome.Succeeded, observer.WorkflowTransitions[^1].CurrentStatus.ExecutionOutcome);
        Assert.Equal(ExecutionOutcome.Succeeded, observer.TaskTransitions[^1].CurrentStatus.ExecutionOutcome);
    }

    [Fact]
    public void RunToCompletion_NotifiesObserverForRuntimeGraphMutations()
    {
        WorkflowSpecification specification = new WorkflowSpecification(
            WorkflowId: new WorkflowId("ObservedDynamicWorkflow"),
            Tasks:
            [
                new TaskSpecification(
                    TaskId: new TaskId("A"),
                    TaskType: "ScanMp4Directory"),
                new TaskSpecification(
                    TaskId: new TaskId("C"),
                    TaskType: "AggregateMp4Results")
            ],
            Dependencies: Array.Empty<TaskDependencySpecification>(),
            MaxConcurrency: 4);

        Workflow workflow = Workflow.FromSpecification(specification);
        RecordingWorkflowObserver observer = new RecordingWorkflowObserver();

        workflow.RunToCompletion(new DynamicFanOutFakeTaskExecutor(), observer);

        Assert.Equal(["B-1", "B-2", "B-3"], observer.TaskAddedEvents.Select(task => task.TaskId.Value));
        Assert.Equal(
            ["B-1->C", "B-2->C", "B-3->C"],
            observer.DependencyAddedEvents
                .Select(dependency => $"{dependency.PrerequisiteTaskId.Value}->{dependency.DependentTaskId.Value}")
                .OrderBy(value => value));
        FanInExpandedEvent fanInEvent = Assert.Single(observer.FanInExpandedEvents);
        Assert.Equal(new TaskId("C"), fanInEvent.JoinTaskId);
        Assert.Equal(["B-1", "B-2", "B-3"], fanInEvent.PrerequisiteTaskIds.Select(taskId => taskId.Value).OrderBy(value => value));
    }

    [Fact]
    public void TextWriterWorkflowObserver_WritesExpectedTransitionAndMutationLines()
    {
        StringWriter writer = new StringWriter();
        TextWriterWorkflowObserver observer = new TextWriterWorkflowObserver(writer);

        observer.OnWorkflowTransition(new WorkflowTransitionEvent(
            WorkflowId: new WorkflowId("W0"),
            PreviousStatus: new WorkflowStatus(new WorkflowId("W0"), ExecutionPhase.NotStarted, ExecutionOutcome.Pending, ExecutionFailureKind.None, ExecutionRecoverability.AwaitingOutcome, new Dictionary<TaskId, TaskStatus>()),
            CurrentStatus: new WorkflowStatus(new WorkflowId("W0"), ExecutionPhase.ReadyToRun, ExecutionOutcome.Pending, ExecutionFailureKind.None, ExecutionRecoverability.AwaitingOutcome, new Dictionary<TaskId, TaskStatus>()),
            Timestamp: DateTime.UtcNow));

        observer.OnTaskTransition(new TaskTransitionEvent(
            WorkflowId: new WorkflowId("W0"),
            TaskId: new TaskId("A"),
            PreviousStatus: new TaskStatus(new WorkflowId("W0"), new TaskId("A")),
            CurrentStatus: new TaskStatus(new WorkflowId("W0"), new TaskId("A"), ExecutionPhase.Running, ExecutionOutcome.Pending, ExecutionFailureKind.None, ExecutionRecoverability.AwaitingOutcome),
            Timestamp: DateTime.UtcNow));

        observer.OnTaskAdded(new TaskAddedEvent(
            WorkflowId: new WorkflowId("W0"),
            TaskId: new TaskId("B"),
            TaskSpecification: new TaskSpecification(new TaskId("B"), "ChildTask"),
            TaskStatus: new TaskStatus(new WorkflowId("W0"), new TaskId("B")),
            Timestamp: DateTime.UtcNow));

        observer.OnDependencyAdded(new DependencyAddedEvent(
            WorkflowId: new WorkflowId("W0"),
            PrerequisiteTaskId: new TaskId("A"),
            DependentTaskId: new TaskId("B"),
            Timestamp: DateTime.UtcNow));

        observer.OnFanInExpanded(new FanInExpandedEvent(
            WorkflowId: new WorkflowId("W0"),
            JoinTaskId: new TaskId("C"),
            PrerequisiteTaskIds: [new TaskId("B-1"), new TaskId("B-2")],
            Timestamp: DateTime.UtcNow));

        string output = writer.ToString();

        Assert.Contains("/W0 ReadyToRun, Pending, None, AwaitingOutcome", output, StringComparison.Ordinal);
        Assert.Contains("/W0/A Running, Pending, None, AwaitingOutcome", output, StringComparison.Ordinal);
        Assert.Contains("/W0/B added by runtime mutation", output, StringComparison.Ordinal);
        Assert.Contains("/W0 dependency added: A -> B", output, StringComparison.Ordinal);
        Assert.Contains("/W0 fan-in expanded for C: [B-1,B-2]", output, StringComparison.Ordinal);
    }

    private sealed class RecordingWorkflowObserver : IWorkflowObserver
    {
        public List<TaskTransitionEvent> TaskTransitions { get; } = new List<TaskTransitionEvent>();

        public List<WorkflowTransitionEvent> WorkflowTransitions { get; } = new List<WorkflowTransitionEvent>();

        public List<TaskAddedEvent> TaskAddedEvents { get; } = new List<TaskAddedEvent>();

        public List<DependencyAddedEvent> DependencyAddedEvents { get; } = new List<DependencyAddedEvent>();

        public List<FanInExpandedEvent> FanInExpandedEvents { get; } = new List<FanInExpandedEvent>();

        public void OnTaskTransition(TaskTransitionEvent transitionEvent)
        {
            TaskTransitions.Add(transitionEvent);
        }

        public void OnWorkflowTransition(WorkflowTransitionEvent transitionEvent)
        {
            WorkflowTransitions.Add(transitionEvent);
        }

        public void OnTaskAdded(TaskAddedEvent taskAddedEvent)
        {
            TaskAddedEvents.Add(taskAddedEvent);
        }

        public void OnDependencyAdded(DependencyAddedEvent dependencyAddedEvent)
        {
            DependencyAddedEvents.Add(dependencyAddedEvent);
        }

        public void OnFanInExpanded(FanInExpandedEvent fanInExpandedEvent)
        {
            FanInExpandedEvents.Add(fanInExpandedEvent);
        }
    }
}