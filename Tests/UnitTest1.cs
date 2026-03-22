namespace OrganizeMedia.Tests;

using OrganizeMedia.Framework;

public class WorkflowRetryTests
{
    [Fact]
    public void RunToCompletion_RerunsRetryableFailedTaskBeforeDependents()
    {
        Workflow workflow = CreateWorkflow(
            workflowId: "RetryFailure",
            taskIds: ["A", "B"],
            dependencies:
            [
                new TaskDependencySpecification(new TaskId("A"), new TaskId("B"))
            ]);

        RecordingTaskExecutor taskExecutor = new RecordingTaskExecutor(
            new Dictionary<string, IReadOnlyList<TaskExecutionResult>>
            {
                ["A"] =
                [
                    TaskExecutionResult.Failed(ExecutionFailureKind.Transient),
                    TaskExecutionResult.Succeeded(new TextExecutionOutput("A complete"))
                ],
                ["B"] =
                [
                    TaskExecutionResult.Succeeded(new TextExecutionOutput("B complete"))
                ]
            });

        workflow.RunToCompletion(taskExecutor);

        Assert.Equal(ExecutionOutcome.Failed, workflow.Status.ExecutionOutcome);
        Assert.Equal(ExecutionRecoverability.Retryable, workflow.Status.Recoverability);
        Assert.Equal(["A"], taskExecutor.ExecutedTaskIds);

        workflow.RunToCompletion(taskExecutor);

        Assert.Equal(ExecutionOutcome.Succeeded, workflow.Status.ExecutionOutcome);
        Assert.Equal(ExecutionRecoverability.NoRecoveryNeeded, workflow.Status.Recoverability);
        Assert.Equal(["A", "A", "B"], taskExecutor.ExecutedTaskIds);
        Assert.Equal(
            ExecutionOutcome.Succeeded,
            workflow.Status.TaskStatuses[new TaskId("A")].ExecutionOutcome);
        Assert.Equal(
            ExecutionOutcome.Succeeded,
            workflow.Status.TaskStatuses[new TaskId("B")].ExecutionOutcome);
    }

    [Fact]
    public void RunToCompletion_RerunsRetryableCanceledTask()
    {
        Workflow workflow = CreateWorkflow(
            workflowId: "RetryCanceled",
            taskIds: ["A"],
            dependencies: Array.Empty<TaskDependencySpecification>());

        RecordingTaskExecutor taskExecutor = new RecordingTaskExecutor(
            new Dictionary<string, IReadOnlyList<TaskExecutionResult>>
            {
                ["A"] =
                [
                    TaskExecutionResult.Canceled(
                        error: new ErrorInfo("Canceled", "try again"),
                        recoverability: ExecutionRecoverability.Retryable),
                    TaskExecutionResult.Succeeded(new TextExecutionOutput("A complete"))
                ]
            });

        workflow.RunToCompletion(taskExecutor);

        Assert.Equal(ExecutionOutcome.Canceled, workflow.Status.ExecutionOutcome);
        Assert.Equal(ExecutionRecoverability.Retryable, workflow.Status.Recoverability);
        Assert.Equal(["A"], taskExecutor.ExecutedTaskIds);

        workflow.RunToCompletion(taskExecutor);

        Assert.Equal(ExecutionOutcome.Succeeded, workflow.Status.ExecutionOutcome);
        Assert.Equal(["A", "A"], taskExecutor.ExecutedTaskIds);
    }

    private static Workflow CreateWorkflow(
        string workflowId,
        IReadOnlyCollection<string> taskIds,
        IReadOnlyCollection<TaskDependencySpecification> dependencies)
    {
        WorkflowSpecification specification = new WorkflowSpecification(
            WorkflowId: new WorkflowId(workflowId),
            Tasks: taskIds
                .Select(taskId => new TaskSpecification(new TaskId(taskId), taskId))
                .ToArray(),
            Dependencies: dependencies.ToArray());

        return Workflow.FromSpecification(specification);
    }

    private sealed class RecordingTaskExecutor : ITaskExecutor
    {
        private readonly Dictionary<string, Queue<TaskExecutionResult>> _resultsByTaskId;

        public RecordingTaskExecutor(Dictionary<string, IReadOnlyList<TaskExecutionResult>> resultsByTaskId)
        {
            _resultsByTaskId = resultsByTaskId.ToDictionary(
                pair => pair.Key,
                pair => new Queue<TaskExecutionResult>(pair.Value));
        }

        public List<string> ExecutedTaskIds { get; } = new List<string>();

        public TaskExecutionResult Execute(IExecutionContext executionContext)
        {
            string taskId = executionContext.TaskId.Value;
            ExecutedTaskIds.Add(taskId);

            if (!_resultsByTaskId.TryGetValue(taskId, out Queue<TaskExecutionResult> results) || results.Count == 0)
            {
                throw new InvalidOperationException($"No configured execution result for task {taskId}.");
            }

            return results.Dequeue();
        }
    }
}
