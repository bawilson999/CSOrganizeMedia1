namespace OrganizeMedia.Framework;

internal static class ExecutionTransitionSupport
{
    internal static bool HasNotStartedExecution(ExecutionPhase phase)
    {
        return phase == ExecutionPhase.NotStarted ||
               phase == ExecutionPhase.ReadyToRun ||
               phase == ExecutionPhase.Queued;
    }

    internal static bool IsRestartRecoverability(ExecutionRecoverability recoverability)
    {
        return recoverability == ExecutionRecoverability.Retryable ||
               recoverability == ExecutionRecoverability.Resumable;
    }

    internal static void EnsureCanMarkReadyToRun(
        string subjectName,
        string subjectId,
        ExecutionPhase currentPhase,
        ExecutionRecoverability currentRecoverability)
    {
        if (currentPhase == ExecutionPhase.NotStarted)
        {
            return;
        }

        if (currentPhase == ExecutionPhase.Finished && IsRestartRecoverability(currentRecoverability))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{subjectName} {subjectId} can only become ready-to-run from NotStarted or a retryable finished state.");
    }

    internal static void EnsurePhase(
        string subjectName,
        string subjectId,
        ExecutionPhase currentPhase,
        params ExecutionPhase[] allowedPhases)
    {
        if (allowedPhases.Contains(currentPhase))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{subjectName} {subjectId} cannot transition from {currentPhase} to the requested state.");
    }

    internal static void EnsureTerminalRecoverability(
        ExecutionRecoverability recoverability,
        string paramName,
        string message)
    {
        if (recoverability == ExecutionRecoverability.Retryable ||
            recoverability == ExecutionRecoverability.Resumable ||
            recoverability == ExecutionRecoverability.RequiresIntervention ||
            recoverability == ExecutionRecoverability.NotRecoverable)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(paramName, recoverability, message);
    }

    internal static ExecutionRecoverability ResolveFailureRecoverability(
        ExecutionFailureKind failureKind,
        ExecutionRecoverability? recoverability,
        string invalidFailureKindMessage,
        string invalidRecoverabilityMessage)
    {
        if (failureKind == ExecutionFailureKind.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                invalidFailureKindMessage);
        }

        ExecutionRecoverability effectiveRecoverability = recoverability ??
            ExecutionRecoverabilityDefaults.From(
                ExecutionPhase.Finished,
                ExecutionOutcome.Failed,
                failureKind);

        EnsureTerminalRecoverability(
            effectiveRecoverability,
            nameof(recoverability),
            invalidRecoverabilityMessage);

        return effectiveRecoverability;
    }
}