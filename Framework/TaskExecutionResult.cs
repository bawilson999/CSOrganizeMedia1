namespace OrganizeMedia.Framework;

public sealed record TaskExecutionResult
{
    private TaskExecutionResult(
        ExecutionOutcome executionOutcome,
        ExecutionFailureKind failureKind = ExecutionFailureKind.None,
        ExecutionOutput output = null,
        ErrorInfo error = null,
        ExecutionRecoverability? recoverability = null,
        TaskRuntimeMutations runtimeMutations = null)
    {
        ExecutionOutcome = executionOutcome;
        FailureKind = failureKind;
        Output = output;
        Error = error;
        Recoverability = recoverability;
        RuntimeMutations = runtimeMutations;
    }

    public ExecutionOutcome ExecutionOutcome { get; }

    public ExecutionFailureKind FailureKind { get; }

    public ExecutionOutput Output { get; }

    public ErrorInfo Error { get; }

    public ExecutionRecoverability? Recoverability { get; }

    public TaskRuntimeMutations RuntimeMutations { get; }

    public IReadOnlyCollection<TaskSpecification> SpawnedTasks =>
        RuntimeMutations?.SpawnedTasks ?? Array.Empty<TaskSpecification>();

    public IReadOnlyCollection<TaskDependencySpecification> AddedDependencies =>
        RuntimeMutations?.AddedDependencies ?? Array.Empty<TaskDependencySpecification>();

    public IReadOnlyCollection<TaskFanInSpecification> FanInSpecifications =>
        RuntimeMutations?.FanInSpecifications ?? Array.Empty<TaskFanInSpecification>();

    internal static TaskExecutionResult SucceededWithMutations(
        ExecutionOutput output = null,
        TaskRuntimeMutations runtimeMutations = null)
    {
        return new TaskExecutionResult(
            executionOutcome: ExecutionOutcome.Succeeded,
            output: output,
            runtimeMutations: runtimeMutations ?? TaskRuntimeMutations.None);
    }

    public static TaskExecutionResult Succeeded(
        ExecutionOutput output = null,
        IReadOnlyCollection<TaskSpecification> spawnedTasks = null,
        IReadOnlyCollection<TaskDependencySpecification> addedDependencies = null,
        IReadOnlyCollection<TaskFanInSpecification> fanInSpecifications = null)
    {
        return SucceededWithMutations(
            output,
            TaskRuntimeMutations.Create(
                spawnedTasks: spawnedTasks,
                addedDependencies: addedDependencies,
                fanInSpecifications: fanInSpecifications));
    }

    public static TaskExecutionResult Canceled(
        ExecutionOutput output = null,
        ErrorInfo error = null,
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
        ExecutionOutput output = null,
        ErrorInfo error = null,
        ExecutionRecoverability? recoverability = null)
    {
        if (failureKind == ExecutionFailureKind.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "Failed task results must use a transient, permanent, or unknown failure kind.");
        }

        return new TaskExecutionResult(
            executionOutcome: ExecutionOutcome.Failed,
            failureKind: failureKind,
            output: output,
            error: error,
            recoverability: recoverability);
    }
}