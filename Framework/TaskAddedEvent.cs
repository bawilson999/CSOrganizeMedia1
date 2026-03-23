namespace OrganizeMedia.Framework;

public record TaskAddedEvent(
    WorkflowSpecificationId WorkflowSpecificationId,
    WorkflowInstanceId WorkflowInstanceId,
    TaskSpecificationId TaskSpecificationId,
    TaskInstanceId TaskInstanceId,
    DateTime Timestamp);