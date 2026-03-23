namespace OrganizeMedia.Framework;

public sealed record TaskExecutionResult
{
    private TaskExecutionResult(
        ExecutionOutcome executionOutcome,
        WorkflowGraphChanges graphChanges,
        ExecutionFailureKind failureKind = ExecutionFailureKind.None,
        ExecutionOutput? output = null,
        ErrorInfo? error = null,
        ExecutionRecoverability? recoverability = null)
    {
        ArgumentNullException.ThrowIfNull(graphChanges);
        Validate(executionOutcome, failureKind, recoverability, graphChanges);

        ExecutionOutcome = executionOutcome;
        FailureKind = failureKind;
        Output = output;
        Error = error;
        Recoverability = recoverability;
        GraphChanges = graphChanges;
    }

    public ExecutionOutcome ExecutionOutcome { get; }

    public ExecutionFailureKind FailureKind { get; }

    public ExecutionOutput? Output { get; }

    public ErrorInfo? Error { get; }

    public ExecutionRecoverability? Recoverability { get; }

    public WorkflowGraphChanges GraphChanges { get; }

    private static void Validate(
        ExecutionOutcome executionOutcome,
        ExecutionFailureKind failureKind,
        ExecutionRecoverability? recoverability,
        WorkflowGraphChanges graphChanges)
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

        if (executionOutcome != ExecutionOutcome.Succeeded && graphChanges != WorkflowGraphChanges.None)
        {
            throw new ArgumentOutOfRangeException(
            nameof(graphChanges),
            "Only succeeded task results can carry non-empty graph changes.");
        }
    }

    public static TaskExecutionResult Succeeded(
        ExecutionOutput? output = null,
        IReadOnlyCollection<TaskSpecification>? spawnedTasks = null,
        IReadOnlyCollection<TaskDependencySpecification>? addedDependencies = null,
        IReadOnlyCollection<TaskSpecificationSpawn>? spawnedTaskSpecifications = null,
        IReadOnlyCollection<TaskInstanceDependency>? addedInstanceDependencies = null)
    {
        return new TaskExecutionResult(
            executionOutcome: ExecutionOutcome.Succeeded,
            graphChanges: WorkflowGraphChanges.Create(spawnedTasks, addedDependencies, spawnedTaskSpecifications, addedInstanceDependencies),
            output: output);
    }

    public static TaskExecutionResult Canceled(
        ExecutionOutput? output = null,
        ErrorInfo? error = null,
        ExecutionRecoverability recoverability = ExecutionRecoverability.Retryable)
    {
        return new TaskExecutionResult(
            executionOutcome: ExecutionOutcome.Canceled,
            graphChanges: WorkflowGraphChanges.None,
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
            graphChanges: WorkflowGraphChanges.None,
            failureKind: failureKind,
            output: output,
            error: error,
            recoverability: recoverability);
    }
}