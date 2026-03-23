namespace OrganizeMedia.Framework;

public static class TaskStatusFormatter
{
    public static string Format(WorkflowId workflowId, TaskInstanceId taskInstanceId, TaskStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return $"/{workflowId}/{taskInstanceId} {status.ExecutionPhase}, {status.ExecutionOutcome}, {status.FailureKind}, {status.Recoverability}";
    }
}