namespace OrganizeMedia.Framework;

public record DependencyAddedEvent(
    WorkflowId WorkflowId,
    TaskInstanceId PrerequisiteTaskInstanceId,
    TaskInstanceId DependentTaskInstanceId,
    DateTime Timestamp);