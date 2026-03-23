namespace OrganizeMedia.Framework;

public static class WorkflowStatusFormatter
{
    public static string Format(WorkflowInstanceId workflowInstanceId, WorkflowStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return $"/{workflowInstanceId} {status.ExecutionPhase}, {status.ExecutionOutcome}, {status.FailureKind}, {status.Recoverability}";
    }
}