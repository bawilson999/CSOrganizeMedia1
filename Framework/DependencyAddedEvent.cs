namespace OrganizeMedia.Framework;

public record DependencyAddedEvent(
    WorkflowId WorkflowId,
    TaskId PrerequisiteTaskId,
    TaskId DependentTaskId,
    DateTime Timestamp);