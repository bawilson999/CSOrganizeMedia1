namespace OrganizeMedia.Framework;

public class Task
{
    private IWorkflowObserver _observer;

    public Task(WorkflowId workflowId, TaskId taskId)
        : this(workflowId, new TaskSpecification(taskId, TaskType: "Task"))
    {
    }

    public Task(WorkflowId workflowId, TaskSpecification specification)
        : this(workflowId, specification, new TaskExecutionState(workflowId, specification.TaskId))
    {
    }

    internal Task(WorkflowId workflowId, TaskSpecification specification, TaskExecutionState executionState)
    {
        ArgumentNullException.ThrowIfNull(specification);

        WorkflowId = workflowId;
        TaskId = specification.TaskId;
        Specification = specification;
        ExecutionState = executionState;
        _observer = NullWorkflowObserver.Instance;
    }

    public WorkflowId WorkflowId { get; init; }

    public TaskId TaskId { get; init; }

    public TaskSpecification Specification { get; init; }

    internal TaskExecutionState ExecutionState { get; init; }

    public TaskStatus Status => ExecutionState.ToStatus();

    public override string ToString()
    {
        return $"/{WorkflowId}/{TaskId}";
    }

    public string ToDisplayString()
    {
        var status = Status;
        return $"/{WorkflowId}/{TaskId} {status.ExecutionPhase}, {status.ExecutionOutcome}, {status.FailureKind}, {status.Recoverability}";
    }

    private static bool IsRestartRecoverability(ExecutionRecoverability recoverability)
    {
        return recoverability == ExecutionRecoverability.Retryable ||
               recoverability == ExecutionRecoverability.Resumable;
    }

    private static bool IsTerminalRecoverability(ExecutionRecoverability recoverability)
    {
        return recoverability == ExecutionRecoverability.Retryable ||
               recoverability == ExecutionRecoverability.Resumable ||
               recoverability == ExecutionRecoverability.RequiresIntervention ||
               recoverability == ExecutionRecoverability.NotRecoverable;
    }

    private void EnsurePhase(params ExecutionPhase[] allowedPhases)
    {
        if (allowedPhases.Contains(ExecutionState.ExecutionPhase))
            return;

        throw new InvalidOperationException(
            $"Task {TaskId} cannot transition from {ExecutionState.ExecutionPhase} to the requested state.");
    }

    public void MarkReadyToRun()
    {
        TaskStatus previousStatus = Status;
        ExecutionPhase currentPhase = ExecutionState.ExecutionPhase;
        ExecutionRecoverability currentRecoverability = ExecutionState.Recoverability;

        if (currentPhase != ExecutionPhase.NotStarted &&
            !(currentPhase == ExecutionPhase.Finished && IsRestartRecoverability(currentRecoverability)))
        {
            throw new InvalidOperationException(
                $"Task {TaskId} can only become ready-to-run from NotStarted or a retryable finished state.");
        }

        bool resetProgress = currentRecoverability != ExecutionRecoverability.Resumable;

        ExecutionState.ExecutionOutcome = ExecutionOutcome.Pending;
        ExecutionState.FailureKind = ExecutionFailureKind.None;
        ExecutionState.Error = null;
        ExecutionState.Output = null;

        if (resetProgress)
            ExecutionState.CompletedSteps = 0;

        ExecutionState.ExecutionPhase = ExecutionPhase.ReadyToRun;
        NotifyTransition(previousStatus);
    }

    public void MarkQueued()
    {
        TaskStatus previousStatus = Status;
        EnsurePhase(ExecutionPhase.ReadyToRun);
        ExecutionState.ExecutionPhase = ExecutionPhase.Queued;
        NotifyTransition(previousStatus);
    }

    public void MarkRunning()
    {
        TaskStatus previousStatus = Status;
        EnsurePhase(ExecutionPhase.Queued);
        ExecutionState.ExecutionPhase = ExecutionPhase.Running;
        NotifyTransition(previousStatus);
    }

    public void MarkSucceeded(ExecutionOutput output = null)
    {
        TaskStatus previousStatus = Status;
        EnsurePhase(ExecutionPhase.Running);
        ExecutionState.ExecutionPhase = ExecutionPhase.Finished;
        ExecutionState.ExecutionOutcome = ExecutionOutcome.Succeeded;
        ExecutionState.FailureKind = ExecutionFailureKind.None;
        ExecutionState.CompletedSteps = ExecutionState.TotalSteps;
        ExecutionState.Error = null;
        ExecutionState.Output = output;
        NotifyTransition(previousStatus);
    }

    public void MarkCanceled(
        ExecutionOutput output = null,
        ErrorInfo error = null,
        ExecutionRecoverability recoverability = ExecutionRecoverability.Retryable)
    {
        TaskStatus previousStatus = Status;
        EnsurePhase(ExecutionPhase.Running);

        if (!IsTerminalRecoverability(recoverability))
        {
            throw new ArgumentOutOfRangeException(
                nameof(recoverability),
                recoverability,
                "Canceled tasks must use a terminal recoverability value.");
        }

        ExecutionState.ExecutionPhase = ExecutionPhase.Finished;
        ExecutionState.ExecutionOutcome = ExecutionOutcome.Canceled;
        ExecutionState.FailureKind = ExecutionFailureKind.None;
        ExecutionState.Error = error;
        ExecutionState.Output = output;
        ExecutionState.Recoverability = recoverability;
        NotifyTransition(previousStatus);
    }

    public void MarkFailed(
        ExecutionFailureKind failureKind,
        ExecutionOutput output = null,
        ErrorInfo error = null,
        ExecutionRecoverability? recoverability = null)
    {
        TaskStatus previousStatus = Status;
        EnsurePhase(ExecutionPhase.Running);

        if (failureKind == ExecutionFailureKind.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "Failed tasks must use a transient, permanent, or unknown failure kind.");
        }

        ExecutionRecoverability effectiveRecoverability = recoverability ??
            ExecutionRecoverabilityDefaults.From(
            ExecutionPhase.Finished,
            ExecutionOutcome.Failed,
            failureKind);

        if (!IsTerminalRecoverability(effectiveRecoverability))
        {
            throw new ArgumentOutOfRangeException(
                nameof(recoverability),
                effectiveRecoverability,
                "Failed tasks must use a terminal recoverability value.");
        }

        ExecutionState.ExecutionPhase = ExecutionPhase.Finished;
        ExecutionState.ExecutionOutcome = ExecutionOutcome.Failed;
        ExecutionState.FailureKind = failureKind;
        ExecutionState.Error = error;
        ExecutionState.Output = output;
        ExecutionState.Recoverability = effectiveRecoverability;
        NotifyTransition(previousStatus);
    }

    public void MarkFailed(
        Exception exception,
        ExecutionFailureKind failureKind,
        ExecutionOutput output = null,
        ExecutionRecoverability? recoverability = null)
    {
        MarkFailed(
            output: output,
            error: ErrorInfo.FromException(exception),
            failureKind: failureKind,
            recoverability: recoverability);
    }

    internal void ResetToNotStarted()
    {
        TaskStatus previousStatus = Status;

        if (ExecutionState.ExecutionPhase == ExecutionPhase.Running ||
            ExecutionState.ExecutionPhase == ExecutionPhase.Finished)
        {
            throw new InvalidOperationException(
                $"Task {TaskId} cannot be reset after execution has started.");
        }

        ExecutionState.ExecutionPhase = ExecutionPhase.NotStarted;
        ExecutionState.ExecutionOutcome = ExecutionOutcome.Pending;
        ExecutionState.FailureKind = ExecutionFailureKind.None;
        ExecutionState.CompletedSteps = 0;
        ExecutionState.Error = null;
        ExecutionState.Output = null;
        NotifyTransition(previousStatus);
    }

    internal void SetObserver(IWorkflowObserver observer)
    {
        _observer = observer ?? NullWorkflowObserver.Instance;
    }

    private void NotifyTransition(TaskStatus previousStatus)
    {
        TaskStatus currentStatus = Status;

        try
        {
            _observer.OnTaskTransition(new TaskTransitionEvent(
                WorkflowId: WorkflowId,
                TaskId: TaskId,
                PreviousStatus: previousStatus,
                CurrentStatus: currentStatus,
                Timestamp: currentStatus.Timestamp ?? DateTime.UtcNow));
        }
        catch
        {
        }
    }
}
