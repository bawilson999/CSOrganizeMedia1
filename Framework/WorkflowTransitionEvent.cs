namespace OrganizeMedia.Framework;

public record WorkflowTransitionEvent(
    WorkflowId WorkflowId,
    WorkflowStatus PreviousStatus,
    WorkflowStatus CurrentStatus,
    DateTime Timestamp);