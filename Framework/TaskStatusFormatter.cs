namespace OrganizeMedia.Framework;

public static class TaskStatusFormatter
{
    public static string Format(WorkflowInstanceId workflowInstanceId, TaskInstanceId taskInstanceId, TaskStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return $"/{workflowInstanceId}/{taskInstanceId} {status.ExecutionPhase}, {status.ExecutionOutcome}, {status.FailureKind}, {status.Recoverability}";
    }
}