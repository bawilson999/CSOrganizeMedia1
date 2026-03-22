namespace OrganizeMedia.Framework;

public record TaskExecutionResult(
    ExecutionOutcome ExecutionOutcome,
    ExecutionFailureKind FailureKind = ExecutionFailureKind.None,
    ExecutionOutput Output = null,
    ErrorInfo Error = null,
    ExecutionRecoverability? Recoverability = null,
    IReadOnlyCollection<TaskSpecification> SpawnedTasks = null,
    IReadOnlyCollection<TaskDependencySpecification> AddedDependencies = null,
    IReadOnlyCollection<TaskFanInSpecification> FanInSpecifications = null)
{
    public static TaskExecutionResult Succeeded(
        ExecutionOutput output = null,
        IReadOnlyCollection<TaskSpecification> spawnedTasks = null,
        IReadOnlyCollection<TaskDependencySpecification> addedDependencies = null,
        IReadOnlyCollection<TaskFanInSpecification> fanInSpecifications = null)
    {
        return new TaskExecutionResult(
            ExecutionOutcome: ExecutionOutcome.Succeeded,
            Output: output,
            SpawnedTasks: spawnedTasks ?? Array.Empty<TaskSpecification>(),
            AddedDependencies: addedDependencies ?? Array.Empty<TaskDependencySpecification>(),
            FanInSpecifications: fanInSpecifications ?? Array.Empty<TaskFanInSpecification>());
    }

    public static TaskExecutionResult Canceled(
        ExecutionOutput output = null,
        ErrorInfo error = null,
        ExecutionRecoverability recoverability = ExecutionRecoverability.Retryable)
    {
        return new TaskExecutionResult(
            ExecutionOutcome: ExecutionOutcome.Canceled,
            Output: output,
            Error: error,
            Recoverability: recoverability,
            SpawnedTasks: Array.Empty<TaskSpecification>(),
            AddedDependencies: Array.Empty<TaskDependencySpecification>(),
            FanInSpecifications: Array.Empty<TaskFanInSpecification>());
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
            ExecutionOutcome: ExecutionOutcome.Failed,
            FailureKind: failureKind,
            Output: output,
            Error: error,
            Recoverability: recoverability,
            SpawnedTasks: Array.Empty<TaskSpecification>(),
            AddedDependencies: Array.Empty<TaskDependencySpecification>(),
            FanInSpecifications: Array.Empty<TaskFanInSpecification>());
    }
}