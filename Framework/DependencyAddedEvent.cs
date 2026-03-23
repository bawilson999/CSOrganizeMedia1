namespace OrganizeMedia.Framework;

public record DependencyAddedEvent(
    WorkflowTemplateId WorkflowTemplateId,
    WorkflowInstanceId WorkflowInstanceId,
    TaskInstanceId PrerequisiteTaskInstanceId,
    TaskInstanceId DependentTaskInstanceId,
    DateTime Timestamp);