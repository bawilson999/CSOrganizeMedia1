namespace OrganizeMedia.Framework;

public record TaskAddedEvent(
    WorkflowId WorkflowId,
    TaskId TaskId,
    DateTime Timestamp);