namespace OrganizeMedia.Framework;

public static class WorkflowStatusFormatter
{
    public static string Format(WorkflowId workflowId, WorkflowStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return $"/{workflowId} {status.ExecutionPhase}, {status.ExecutionOutcome}, {status.FailureKind}, {status.Recoverability}";
    }
}