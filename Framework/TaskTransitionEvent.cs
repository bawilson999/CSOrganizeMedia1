namespace OrganizeMedia.Framework;

public record TaskTransitionEvent(
    WorkflowId WorkflowId,
    TaskId TaskId,
    TaskInstanceId TaskInstanceId,
    TaskStatus PreviousStatus,
    TaskStatus CurrentStatus,
    DateTime Timestamp);