namespace OrganizeMedia.Framework;

public static class TaskStatusFormatter
{
    public static string Format(WorkflowId workflowId, TaskId taskId, TaskStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return $"/{workflowId}/{taskId} {status.ExecutionPhase}, {status.ExecutionOutcome}, {status.FailureKind}, {status.Recoverability}";
    }
}