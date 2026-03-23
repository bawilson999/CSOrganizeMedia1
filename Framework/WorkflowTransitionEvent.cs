namespace OrganizeMedia.Framework;

public record WorkflowTransitionEvent(
    WorkflowTemplateId WorkflowTemplateId,
    WorkflowInstanceId WorkflowInstanceId,
    WorkflowStatus PreviousStatus,
    WorkflowStatus CurrentStatus,
    DateTime Timestamp);