namespace OrganizeMedia.Framework;

public record TaskAddedEvent(
    WorkflowId WorkflowId,
    TaskId TaskId,
    TaskInstanceId TaskInstanceId,
    DateTime Timestamp);