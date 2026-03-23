namespace OrganizeMedia.Framework;

public record WorkflowStatus(
    WorkflowId WorkflowId,
    ExecutionPhase ExecutionPhase,
    ExecutionOutcome ExecutionOutcome,
    ExecutionFailureKind FailureKind,
    ExecutionRecoverability Recoverability,
    Dictionary<TaskInstanceId, TaskStatus> TaskStatuses,
    ErrorInfo? Error = null,
    ExecutionOutput? Output = null,
    DateTime? Timestamp = null,
    DateTime? CreatedTimestamp = null,
    DateTime? QueuedTimestamp = null,
    DateTime? ReadyToRunTimestamp = null,
    DateTime? RunningTimestamp = null,
    DateTime? FinishedTimestamp = null)
{
}
