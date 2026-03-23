namespace OrganizeMedia.Framework;

public record TaskTransitionEvent(
    WorkflowTemplateId WorkflowTemplateId,
    WorkflowInstanceId WorkflowInstanceId,
    TaskTemplateId TaskTemplateId,
    TaskInstanceId TaskInstanceId,
    TaskStatus PreviousStatus,
    TaskStatus CurrentStatus,
    DateTime Timestamp);