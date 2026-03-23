namespace OrganizeMedia.Tests;

using OrganizeMedia.Framework;

public class WorkflowExamplesTests
{
    [Fact]
    public void RunToCompletion_ProgramExampleWorkflow_CompletesInDependencySafeOrder()
    {
        Workflow workflow = WorkflowTestSupport.FromAdjacencyArray("W0", [[1], [], [1], [2], [2]]);

        RecordingTaskExecutor taskExecutor = new RecordingTaskExecutor(
            new Dictionary<string, IReadOnlyList<TaskExecutionResult>>
            {
                ["T0"] = [TaskExecutionResult.Succeeded(new TextExecutionOutput("T0 complete"))],
                ["T1"] = [TaskExecutionResult.Succeeded(new TextExecutionOutput("T1 complete"))],
                ["T2"] = [TaskExecutionResult.Succeeded(new TextExecutionOutput("T2 complete"))],
                ["T3"] = [TaskExecutionResult.Succeeded(new TextExecutionOutput("T3 complete"))],
                ["T4"] = [TaskExecutionResult.Succeeded(new TextExecutionOutput("T4 complete"))]
            });

        workflow.RunToCompletion(taskExecutor);

        Assert.Equal(ExecutionOutcome.Succeeded, workflow.Status.ExecutionOutcome);
        Assert.Equal(["T0", "T3", "T4", "T2", "T1"], taskExecutor.ExecutedTaskIds);
        Assert.All(
            workflow.Status.TaskStatuses.Values,
            taskStatus => Assert.Equal(ExecutionOutcome.Succeeded, taskStatus.ExecutionOutcome));
    }

    [Fact]
    public void RunToCompletion_DynamicFanOutAndFanIn_ExecutesJoinAfterSpawnedTasks()
    {
        WorkflowSpecification specification = new WorkflowSpecification(
            WorkflowId: new WorkflowId("Mp4Workflow"),
            Tasks:
            [
                new TaskSpecification(
                    TaskId: new TaskId("A"),
                    TaskType: "ScanMp4Directory",
                    InputType: "application/json",
                    InputJson: "{ \"path\": \"c:/media\" }"),
                new TaskSpecification(
                    TaskId: new TaskId("C"),
                    TaskType: "AggregateMp4Results")
            ],
            Dependencies: Array.Empty<TaskDependencySpecification>(),
            MaxConcurrency: 4);

        Workflow workflow = Workflow.FromSpecification(specification);
        DynamicFanOutTaskExecutor taskExecutor = new DynamicFanOutTaskExecutor();

        workflow.RunToCompletion(taskExecutor);

        Assert.Equal(ExecutionOutcome.Succeeded, workflow.Status.ExecutionOutcome);
        Assert.Equal(["A", "B-1", "B-2", "B-3", "C"], taskExecutor.ExecutedTaskIds);
        Assert.Equal(new TaskId("A"), taskExecutor.SpawnedByTaskIds["B-1"]);
        Assert.Equal(new TaskId("A"), taskExecutor.SpawnedByTaskIds["B-2"]);
        Assert.Equal(new TaskId("A"), taskExecutor.SpawnedByTaskIds["B-3"]);
        Assert.Equal(["B-1", "B-2", "B-3"], taskExecutor.AggregatorDependencyTaskIds);
        Assert.Equal(["processed:a.mp4", "processed:b.mp4", "processed:c.mp4"], taskExecutor.AggregatorDependencyOutputValues);
        Assert.Equal(5, workflow.Status.TaskStatuses.Count);
        Assert.Equal(
            ExecutionOutcome.Succeeded,
            workflow.Status.TaskStatuses[new TaskId("C")].ExecutionOutcome);
    }
}