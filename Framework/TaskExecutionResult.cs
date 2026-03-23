namespace OrganizeMedia.Framework;

public sealed record TaskExecutionResult
{
    private TaskExecutionResult(
        ExecutionOutcome executionOutcome,
        ExecutionFailureKind failureKind = ExecutionFailureKind.None,
        ExecutionOutput? output = null,
        ErrorInfo? error = null,
        ExecutionRecoverability? recoverability = null,
        TaskRuntimeMutations? runtimeMutations = null)
    {
        Validate(executionOutcome, failureKind, recoverability, runtimeMutations);

        ExecutionOutcome = executionOutcome;
        FailureKind = failureKind;
        Output = output;
        Error = error;
        Recoverability = recoverability;
        RuntimeMutations = runtimeMutations;
    }

    public ExecutionOutcome ExecutionOutcome { get; }

    public ExecutionFailureKind FailureKind { get; }

    public ExecutionOutput? Output { get; }

    public ErrorInfo? Error { get; }

    public ExecutionRecoverability? Recoverability { get; }

    public TaskRuntimeMutations? RuntimeMutations { get; }

    public IReadOnlyCollection<TaskSpecification> SpawnedTasks =>
        RuntimeMutations?.SpawnedTasks ?? Array.Empty<TaskSpecification>();

    public IReadOnlyCollection<TaskDependencySpecification> AddedDependencies =>
        RuntimeMutations?.AddedDependencies ?? Array.Empty<TaskDependencySpecification>();

    public IReadOnlyCollection<TaskFanInSpecification> FanInSpecifications =>
        RuntimeMutations?.FanInSpecifications ?? Array.Empty<TaskFanInSpecification>();

    private static void Validate(
        ExecutionOutcome executionOutcome,
        ExecutionFailureKind failureKind,
        ExecutionRecoverability? recoverability,
        TaskRuntimeMutations? runtimeMutations)
    {
        switch (executionOutcome)
        {
            case ExecutionOutcome.Succeeded:
                if (failureKind != ExecutionFailureKind.None)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(failureKind),
                        failureKind,
                        "Succeeded task results cannot carry a failure kind.");
                }

                if (recoverability is not null)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(recoverability),
                        recoverability,
                        "Succeeded task results cannot carry an explicit recoverability value.");
                }

                break;

            case ExecutionOutcome.Canceled:
                if (failureKind != ExecutionFailureKind.None)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(failureKind),
                        failureKind,
                        "Canceled task results cannot carry a failure kind.");
                }

                ExecutionTransitionSupport.EnsureTerminalRecoverability(
                    recoverability ?? ExecutionRecoverability.Retryable,
                    nameof(recoverability),
                    "Canceled task results must use a terminal recoverability value.");
                break;

            case ExecutionOutcome.Failed:
                if (failureKind == ExecutionFailureKind.None)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(failureKind),
                        failureKind,
                        "Failed task results must use a transient, permanent, or unknown failure kind.");
                }

                if (recoverability is not null)
                {
                    ExecutionTransitionSupport.EnsureTerminalRecoverability(
                        recoverability.Value,
                        nameof(recoverability),
                        "Failed task results must use a terminal recoverability value.");
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(executionOutcome),
                    executionOutcome,
                    "Task execution results must use a supported terminal outcome.");
        }

        if (executionOutcome != ExecutionOutcome.Succeeded && runtimeMutations is not null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runtimeMutations),
                "Only succeeded task results can carry runtime mutations.");
        }
    }

    internal static TaskExecutionResult SucceededWithMutations(
        ExecutionOutput? output = null,
        TaskRuntimeMutations? runtimeMutations = null)
    {
        return new TaskExecutionResult(
            executionOutcome: ExecutionOutcome.Succeeded,
            output: output,
            runtimeMutations: runtimeMutations ?? TaskRuntimeMutations.None);
    }

    public static TaskExecutionResult Succeeded(
        ExecutionOutput? output = null,
        IReadOnlyCollection<TaskSpecification>? spawnedTasks = null,
        IReadOnlyCollection<TaskDependencySpecification>? addedDependencies = null,
        IReadOnlyCollection<TaskFanInSpecification>? fanInSpecifications = null)
    {
        return SucceededWithMutations(
            output,
            TaskRuntimeMutations.Create(
                spawnedTasks: spawnedTasks,
                addedDependencies: addedDependencies,
                fanInSpecifications: fanInSpecifications));
    }

    public static TaskExecutionResult Canceled(
        ExecutionOutput? output = null,
        ErrorInfo? error = null,
        ExecutionRecoverability recoverability = ExecutionRecoverability.Retryable)
    {
        return new TaskExecutionResult(
            executionOutcome: ExecutionOutcome.Canceled,
            output: output,
            error: error,
            recoverability: recoverability);
    }

    public static TaskExecutionResult Failed(
        ExecutionFailureKind failureKind,
        ExecutionOutput? output = null,
        ErrorInfo? error = null,
        ExecutionRecoverability? recoverability = null)
    {
        return new TaskExecutionResult(
            executionOutcome: ExecutionOutcome.Failed,
            failureKind: failureKind,
            output: output,
            error: error,
            recoverability: recoverability);
    }
}