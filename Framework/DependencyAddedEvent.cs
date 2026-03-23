namespace OrganizeMedia.Framework;

public record DependencyAddedEvent(
    WorkflowSpecificationId WorkflowSpecificationId,
    WorkflowInstanceId WorkflowInstanceId,
    TaskInstanceId PrerequisiteTaskInstanceId,
    TaskInstanceId DependentTaskInstanceId,
    DateTime Timestamp);