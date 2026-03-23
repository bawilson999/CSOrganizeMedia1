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
            transition => Assert.Equal(new TaskSpecificationId("A"), transition.TaskSpecificationId));
        Assert.All(
            observer.TaskTransitions,
            transition => Assert.Equal(new TaskInstanceId(new TaskSpecificationId("A"), 1), transition.TaskInstanceId));
        Assert.Equal(ExecutionOutcome.Pending, observer.WorkflowTransitions[0].PreviousStatus.ExecutionOutcome);
        Assert.Equal(ExecutionOutcome.Succeeded, observer.WorkflowTransitions[^1].CurrentStatus.ExecutionOutcome);
        Assert.Equal(ExecutionOutcome.Succeeded, observer.TaskTransitions[^1].CurrentStatus.ExecutionOutcome);
    }

    [Fact]
    public void RunToCompletion_NotifiesObserverForRuntimeGraphChanges()
    {
        WorkflowSpecification specification = new WorkflowSpecification(
            WorkflowSpecificationId: new WorkflowSpecificationId("ObservedDynamicWorkflow"),
            Tasks:
            [
                new TaskSpecification(
                    TaskSpecificationId: new TaskSpecificationId("A"),
                    TaskType: new TaskType("ScanMp4Directory")),
                new TaskSpecification(
                    TaskSpecificationId: new TaskSpecificationId("C"),
                    TaskType: new TaskType("AggregateMp4Results"))
            ],
            Dependencies: Array.Empty<TaskDependencySpecification>(),
            MaxConcurrency: 4);

        Workflow workflow = Workflow.FromSpecification(specification);
        RecordingWorkflowObserver observer = new RecordingWorkflowObserver();

        workflow.RunToCompletion(new DynamicSpawnAndJoinFakeTaskExecutor(), observer);

        Assert.Equal(["B-1/1", "B-2/1", "B-3/1"], observer.TaskAddedEvents.Select(task => task.TaskInstanceId.ToString()));
        Assert.Equal(
            ["B-1/1->C/1", "B-2/1->C/1", "B-3/1->C/1"],
            observer.DependencyAddedEvents
                .Select(dependency => $"{dependency.PrerequisiteTaskInstanceId}->{dependency.DependentTaskInstanceId}")
                .OrderBy(value => value));
    }

    [Fact]
    public void TextWriterWorkflowObserver_WritesExpectedTransitionAndGraphChangeLines()
    {
        WorkflowSpecificationId workflowSpecificationId = new WorkflowSpecificationId("W0");
        WorkflowInstanceId workflowInstanceId = new WorkflowInstanceId(workflowSpecificationId, 1);

        StringWriter writer = new StringWriter();
        TextWriterWorkflowObserver observer = new TextWriterWorkflowObserver(writer);

        observer.OnWorkflowTransition(new WorkflowTransitionEvent(
            WorkflowSpecificationId: workflowSpecificationId,
            WorkflowInstanceId: workflowInstanceId,
            PreviousStatus: new WorkflowStatus(workflowSpecificationId, workflowInstanceId, ExecutionPhase.NotStarted, ExecutionOutcome.Pending, ExecutionFailureKind.None, ExecutionRecoverability.AwaitingOutcome, new Dictionary<TaskInstanceId, TaskStatus>()),
            CurrentStatus: new WorkflowStatus(workflowSpecificationId, workflowInstanceId, ExecutionPhase.ReadyToRun, ExecutionOutcome.Pending, ExecutionFailureKind.None, ExecutionRecoverability.AwaitingOutcome, new Dictionary<TaskInstanceId, TaskStatus>()),
            Timestamp: DateTime.UtcNow));

        observer.OnTaskTransition(new TaskTransitionEvent(
            WorkflowSpecificationId: workflowSpecificationId,
            WorkflowInstanceId: workflowInstanceId,
            TaskSpecificationId: new TaskSpecificationId("A"),
            TaskInstanceId: new TaskInstanceId(new TaskSpecificationId("A"), 1),
            PreviousStatus: new TaskStatus(workflowSpecificationId, workflowInstanceId, new TaskSpecificationId("A"), new TaskInstanceId(new TaskSpecificationId("A"), 1)),
            CurrentStatus: new TaskStatus(workflowSpecificationId, workflowInstanceId, new TaskSpecificationId("A"), new TaskInstanceId(new TaskSpecificationId("A"), 1), ExecutionPhase.Running, ExecutionOutcome.Pending, ExecutionFailureKind.None, ExecutionRecoverability.AwaitingOutcome),
            Timestamp: DateTime.UtcNow));

        observer.OnTaskAdded(new TaskAddedEvent(
            WorkflowSpecificationId: workflowSpecificationId,
            WorkflowInstanceId: workflowInstanceId,
            TaskSpecificationId: new TaskSpecificationId("B"),
            TaskInstanceId: new TaskInstanceId(new TaskSpecificationId("B"), 1),
            Timestamp: DateTime.UtcNow));

        observer.OnDependencyAdded(new DependencyAddedEvent(
            WorkflowSpecificationId: workflowSpecificationId,
            WorkflowInstanceId: workflowInstanceId,
            PrerequisiteTaskInstanceId: new TaskInstanceId(new TaskSpecificationId("A"), 1),
            DependentTaskInstanceId: new TaskInstanceId(new TaskSpecificationId("B"), 1),
            Timestamp: DateTime.UtcNow));

        string output = writer.ToString();

        Assert.Contains("/W0/1 ReadyToRun, Pending, None, AwaitingOutcome", output, StringComparison.Ordinal);
        Assert.Contains("/W0/1/A/1 Running, Pending, None, AwaitingOutcome", output, StringComparison.Ordinal);
        Assert.Contains("/W0/1/B/1 added by runtime graph change", output, StringComparison.Ordinal);
        Assert.Contains("/W0/1 dependency added: A/1 -> B/1", output, StringComparison.Ordinal);
    }

    private sealed class RecordingWorkflowObserver : IWorkflowObserver
    {
        public List<TaskTransitionEvent> TaskTransitions { get; } = new List<TaskTransitionEvent>();

        public List<WorkflowTransitionEvent> WorkflowTransitions { get; } = new List<WorkflowTransitionEvent>();

        public List<TaskAddedEvent> TaskAddedEvents { get; } = new List<TaskAddedEvent>();

        public List<DependencyAddedEvent> DependencyAddedEvents { get; } = new List<DependencyAddedEvent>();

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
    }
}