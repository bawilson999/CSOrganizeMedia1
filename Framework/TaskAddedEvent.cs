namespace OrganizeMedia.Framework;

public record TaskAddedEvent(
    WorkflowTemplateId WorkflowTemplateId,
    WorkflowInstanceId WorkflowInstanceId,
    TaskTemplateId TaskTemplateId,
    TaskInstanceId TaskInstanceId,
    DateTime Timestamp);