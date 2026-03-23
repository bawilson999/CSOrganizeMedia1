namespace OrganizeMedia.Framework;

internal static class ExecutionDisplayFormatter
{
    internal static string FormatTaskStatus(WorkflowId workflowId, TaskId taskId, TaskStatus status)
    {
        return $"/{workflowId}/{taskId} {status.ExecutionPhase}, {status.ExecutionOutcome}, {status.FailureKind}, {status.Recoverability}";
    }

    internal static string FormatWorkflowStatus(WorkflowId workflowId, WorkflowStatus status)
    {
        return $"/{workflowId} {status.ExecutionPhase}, {status.ExecutionOutcome}, {status.FailureKind}, {status.Recoverability}";
    }
}