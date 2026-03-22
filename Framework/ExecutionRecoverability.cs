namespace OrganizeMedia.Framework;

public enum ExecutionRecoverability
{
    AwaitingOutcome,
    NoRecoveryNeeded,
    Retryable,
    Resumable,
    RequiresIntervention,
    NotRecoverable,
    Unknown = -1,
}

public static class ExecutionRecoverabilityDefaults
{
    public static ExecutionRecoverability From(
        ExecutionPhase executionPhase,
        ExecutionOutcome executionOutcome,
        ExecutionFailureKind failureKind = ExecutionFailureKind.None)
    {
        if (executionPhase != ExecutionPhase.Finished)
            return ExecutionRecoverability.AwaitingOutcome;

        return executionOutcome switch
        {
            ExecutionOutcome.Pending => ExecutionRecoverability.AwaitingOutcome,
            ExecutionOutcome.Succeeded => ExecutionRecoverability.NoRecoveryNeeded,
            ExecutionOutcome.Canceled => ExecutionRecoverability.Retryable,
            ExecutionOutcome.Failed => failureKind switch
            {
                ExecutionFailureKind.Transient => ExecutionRecoverability.Retryable,
                ExecutionFailureKind.Permanent => ExecutionRecoverability.NotRecoverable,
                ExecutionFailureKind.None => ExecutionRecoverability.RequiresIntervention,
                ExecutionFailureKind.Unknown => ExecutionRecoverability.RequiresIntervention,
                _ => ExecutionRecoverability.Unknown,
            },
            _ => ExecutionRecoverability.Unknown,
        };
    }
}