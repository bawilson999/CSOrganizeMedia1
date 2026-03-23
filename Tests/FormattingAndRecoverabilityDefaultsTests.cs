namespace OrganizeMedia.Tests;

using OrganizeMedia.Framework;

public class FormattingAndRecoverabilityDefaultsTests
{
    [Fact]
    public void TaskStatusFormatter_FormatsExpectedDisplayLine()
    {
        WorkflowSpecificationId workflowSpecificationId = new WorkflowSpecificationId("W0");
        WorkflowInstanceId workflowInstanceId = new WorkflowInstanceId(workflowSpecificationId, 1);

        string formatted = TaskStatusFormatter.Format(
            workflowInstanceId: workflowInstanceId,
            taskInstanceId: new TaskInstanceId(new TaskSpecificationId("A"), 1),
            status: new TaskStatus(
                WorkflowSpecificationId: workflowSpecificationId,
                WorkflowInstanceId: workflowInstanceId,
                TaskSpecificationId: new TaskSpecificationId("A"),
                TaskInstanceId: new TaskInstanceId(new TaskSpecificationId("A"), 1),
                ExecutionPhase: ExecutionPhase.Running,
                ExecutionOutcome: ExecutionOutcome.Pending,
                FailureKind: ExecutionFailureKind.None,
                Recoverability: ExecutionRecoverability.AwaitingOutcome));

        Assert.Equal("/W0/1/A/1 Running, Pending, None, AwaitingOutcome", formatted);
    }

    [Fact]
    public void WorkflowStatusFormatter_FormatsExpectedDisplayLine()
    {
        WorkflowSpecificationId workflowSpecificationId = new WorkflowSpecificationId("W0");
        WorkflowInstanceId workflowInstanceId = new WorkflowInstanceId(workflowSpecificationId, 1);

        string formatted = WorkflowStatusFormatter.Format(
            workflowInstanceId: workflowInstanceId,
            status: new WorkflowStatus(
                WorkflowSpecificationId: workflowSpecificationId,
                WorkflowInstanceId: workflowInstanceId,
                ExecutionPhase: ExecutionPhase.ReadyToRun,
                ExecutionOutcome: ExecutionOutcome.Pending,
                FailureKind: ExecutionFailureKind.None,
                Recoverability: ExecutionRecoverability.AwaitingOutcome,
                TaskStatuses: new Dictionary<TaskInstanceId, TaskStatus>()));

        Assert.Equal("/W0/1 ReadyToRun, Pending, None, AwaitingOutcome", formatted);
    }

    [Fact]
    public void ExecutionRecoverabilityDefaults_ReturnsAwaitingOutcomeBeforeFinished()
    {
        ExecutionRecoverability recoverability = ExecutionRecoverabilityDefaults.From(
            executionPhase: ExecutionPhase.Running,
            executionOutcome: ExecutionOutcome.Failed,
            failureKind: ExecutionFailureKind.Transient);

        Assert.Equal(ExecutionRecoverability.AwaitingOutcome, recoverability);
    }

    [Fact]
    public void ExecutionRecoverabilityDefaults_ReturnsExpectedFinishedMappings()
    {
        Assert.Equal(
            ExecutionRecoverability.NoRecoveryNeeded,
            ExecutionRecoverabilityDefaults.From(ExecutionPhase.Finished, ExecutionOutcome.Succeeded));

        Assert.Equal(
            ExecutionRecoverability.Retryable,
            ExecutionRecoverabilityDefaults.From(ExecutionPhase.Finished, ExecutionOutcome.Canceled));

        Assert.Equal(
            ExecutionRecoverability.Retryable,
            ExecutionRecoverabilityDefaults.From(ExecutionPhase.Finished, ExecutionOutcome.Failed, ExecutionFailureKind.Transient));

        Assert.Equal(
            ExecutionRecoverability.NotRecoverable,
            ExecutionRecoverabilityDefaults.From(ExecutionPhase.Finished, ExecutionOutcome.Failed, ExecutionFailureKind.Permanent));

        Assert.Equal(
            ExecutionRecoverability.RequiresIntervention,
            ExecutionRecoverabilityDefaults.From(ExecutionPhase.Finished, ExecutionOutcome.Failed, ExecutionFailureKind.Unknown));
    }
}