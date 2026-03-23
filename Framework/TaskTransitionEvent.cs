namespace OrganizeMedia.Framework;

public record TaskTransitionEvent(
    WorkflowSpecificationId WorkflowSpecificationId,
    WorkflowInstanceId WorkflowInstanceId,
    TaskSpecificationId TaskSpecificationId,
    TaskInstanceId TaskInstanceId,
    TaskStatus PreviousStatus,
    TaskStatus CurrentStatus,
    DateTime Timestamp);