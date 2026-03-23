namespace OrganizeMedia.Tests;

using OrganizeMedia.Framework;

public class FormattingAndRecoverabilityDefaultsTests
{
    [Fact]
    public void TaskStatusFormatter_FormatsExpectedDisplayLine()
    {
        string formatted = TaskStatusFormatter.Format(
            workflowId: new WorkflowId("W0"),
            taskId: new TaskId("A"),
            status: new TaskStatus(
                WorkflowId: new WorkflowId("W0"),
                TaskId: new TaskId("A"),
                ExecutionPhase: ExecutionPhase.Running,
                ExecutionOutcome: ExecutionOutcome.Pending,
                FailureKind: ExecutionFailureKind.None,
                Recoverability: ExecutionRecoverability.AwaitingOutcome));

        Assert.Equal("/W0/A Running, Pending, None, AwaitingOutcome", formatted);
    }

    [Fact]
    public void WorkflowStatusFormatter_FormatsExpectedDisplayLine()
    {
        string formatted = WorkflowStatusFormatter.Format(
            workflowId: new WorkflowId("W0"),
            status: new WorkflowStatus(
                WorkflowId: new WorkflowId("W0"),
                ExecutionPhase: ExecutionPhase.ReadyToRun,
                ExecutionOutcome: ExecutionOutcome.Pending,
                FailureKind: ExecutionFailureKind.None,
                Recoverability: ExecutionRecoverability.AwaitingOutcome,
                TaskStatuses: new Dictionary<TaskId, TaskStatus>()));

        Assert.Equal("/W0 ReadyToRun, Pending, None, AwaitingOutcome", formatted);
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