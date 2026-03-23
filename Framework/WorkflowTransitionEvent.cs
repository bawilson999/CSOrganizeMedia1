namespace OrganizeMedia.Framework;

public record WorkflowTransitionEvent(
    WorkflowSpecificationId WorkflowSpecificationId,
    WorkflowInstanceId WorkflowInstanceId,
    WorkflowStatus PreviousStatus,
    WorkflowStatus CurrentStatus,
    DateTime Timestamp);