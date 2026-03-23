namespace OrganizeMedia.Framework;

public record TaskStatus(
    WorkflowTemplateId WorkflowTemplateId,
    WorkflowInstanceId WorkflowInstanceId,
    TaskTemplateId TaskTemplateId,
    TaskInstanceId TaskInstanceId,
    ExecutionPhase ExecutionPhase = ExecutionPhase.NotStarted,
    ExecutionOutcome ExecutionOutcome = ExecutionOutcome.Pending,
    ExecutionFailureKind FailureKind = ExecutionFailureKind.None,
    ExecutionRecoverability Recoverability = ExecutionRecoverability.AwaitingOutcome,
    int TotalSteps = 1,
    int CompletedSteps = 0,
    ErrorInfo? Error = null,
    ExecutionOutput? Output = null,
    DateTime? Timestamp = null,
    DateTime? CreatedTimestamp = null,
    DateTime? QueuedTimestamp = null,
    DateTime? ReadyToRunTimestamp = null,
    DateTime? RunningTimestamp = null,
    DateTime? FinishedTimestamp = null,
    TaskInstanceId? SpawnedByTaskInstanceId = null);
