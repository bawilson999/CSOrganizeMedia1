namespace OrganizeMedia.Tests;

using OrganizeMedia.Framework;

public class WorkflowRetryTests
{
    [Fact]
    public void RunToCompletion_RerunsRetryableFailedTaskBeforeDependents()
    {
        Workflow workflow = WorkflowTestSupport.CreateWorkflow(
            workflowId: "RetryFailure",
            taskIds: ["A", "B"],
            dependencies:
            [
                new TaskDependencySpecification(new TaskId("A"), new TaskId("B"))
            ]);

        RecordingFakeTaskExecutor taskExecutor = new RecordingFakeTaskExecutor(
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
        Workflow workflow = WorkflowTestSupport.CreateWorkflow(
            workflowId: "RetryCanceled",
            taskIds: ["A"],
            dependencies: Array.Empty<TaskDependencySpecification>());

        RecordingFakeTaskExecutor taskExecutor = new RecordingFakeTaskExecutor(
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
}