namespace OrganizeMedia.Framework;

public record FanInExpandedEvent(
    WorkflowId WorkflowId,
    TaskId JoinTaskId,
    IReadOnlyCollection<TaskId> PrerequisiteTaskIds,
    DateTime Timestamp);