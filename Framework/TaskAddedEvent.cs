namespace OrganizeMedia.Framework;

public record TaskAddedEvent(
    WorkflowId WorkflowId,
    TaskId TaskId,
    TaskSpecification TaskSpecification,
    TaskStatus TaskStatus,
    DateTime Timestamp);