namespace OrganizeMedia.Framework;

public record TaskTransitionEvent(
    WorkflowId WorkflowId,
    TaskId TaskId,
    TaskStatus PreviousStatus,
    TaskStatus CurrentStatus,
    DateTime Timestamp);