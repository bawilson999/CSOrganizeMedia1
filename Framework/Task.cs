namespace OrganizeMedia.Framework;

public class Task
{
    private IWorkflowObserver _observer;

    internal Task(WorkflowId workflowId, TaskId taskId)
        : this(workflowId, new TaskSpecification(taskId, TaskType: new TaskType("Task")))
    {
    }

    internal Task(WorkflowId workflowId, TaskSpecification specification)
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

    internal bool IsCompleteForWorkflowSuccess()
    {
        return Status.ExecutionPhase == ExecutionPhase.Finished &&
               !ExecutionTransitionSupport.IsRestartRecoverability(Status.Recoverability);
    }

    internal bool IsSchedulable()
    {
        return Status.ExecutionPhase == ExecutionPhase.NotStarted ||
               (Status.ExecutionPhase == ExecutionPhase.Finished &&
                ExecutionTransitionSupport.IsRestartRecoverability(Status.Recoverability));
    }

    internal bool HasSatisfiedDependencyExecution()
    {
        return Status.ExecutionPhase == ExecutionPhase.Finished &&
               !ExecutionTransitionSupport.IsRestartRecoverability(Status.Recoverability);
    }

    internal void MarkReadyAndQueued()
    {
        MarkReadyToRun();
        MarkQueued();
    }

    internal void MarkReadyToRun()
    {
        TaskStatus previousStatus = Status;
        ExecutionRecoverability currentRecoverability = ExecutionState.Recoverability;
        ExecutionTransitionSupport.EnsureCanMarkReadyToRun(
            subjectName: "Task",
            subjectId: TaskId.Value,
            currentPhase: ExecutionState.ExecutionPhase,
            currentRecoverability: currentRecoverability);

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

    internal void MarkQueued()
    {
        TaskStatus previousStatus = Status;
        ExecutionTransitionSupport.EnsurePhase(
            subjectName: "Task",
            subjectId: TaskId.Value,
            currentPhase: ExecutionState.ExecutionPhase,
            ExecutionPhase.ReadyToRun);
        ExecutionState.ExecutionPhase = ExecutionPhase.Queued;
        NotifyTransition(previousStatus);
    }

    internal void MarkRunning()
    {
        TaskStatus previousStatus = Status;
        ExecutionTransitionSupport.EnsurePhase(
            subjectName: "Task",
            subjectId: TaskId.Value,
            currentPhase: ExecutionState.ExecutionPhase,
            ExecutionPhase.Queued);
        ExecutionState.ExecutionPhase = ExecutionPhase.Running;
        NotifyTransition(previousStatus);
    }

    internal void MarkSucceeded(ExecutionOutput? output = null)
    {
        TaskStatus previousStatus = Status;
        ExecutionTransitionSupport.EnsurePhase(
            subjectName: "Task",
            subjectId: TaskId.Value,
            currentPhase: ExecutionState.ExecutionPhase,
            ExecutionPhase.Running);
        ExecutionState.ExecutionPhase = ExecutionPhase.Finished;
        ExecutionState.ExecutionOutcome = ExecutionOutcome.Succeeded;
        ExecutionState.FailureKind = ExecutionFailureKind.None;
        ExecutionState.CompletedSteps = ExecutionState.TotalSteps;
        ExecutionState.Error = null;
        ExecutionState.Output = output;
        NotifyTransition(previousStatus);
    }

    internal void MarkCanceled(
        ExecutionOutput? output = null,
        ErrorInfo? error = null,
        ExecutionRecoverability recoverability = ExecutionRecoverability.Retryable)
    {
        TaskStatus previousStatus = Status;
        ExecutionTransitionSupport.EnsurePhase(
            subjectName: "Task",
            subjectId: TaskId.Value,
            currentPhase: ExecutionState.ExecutionPhase,
            ExecutionPhase.Running);
        ExecutionTransitionSupport.EnsureTerminalRecoverability(
            recoverability,
            nameof(recoverability),
            "Canceled tasks must use a terminal recoverability value.");

        ExecutionState.ExecutionPhase = ExecutionPhase.Finished;
        ExecutionState.ExecutionOutcome = ExecutionOutcome.Canceled;
        ExecutionState.FailureKind = ExecutionFailureKind.None;
        ExecutionState.Error = error;
        ExecutionState.Output = output;
        ExecutionState.Recoverability = recoverability;
        NotifyTransition(previousStatus);
    }

    internal void MarkFailed(
        ExecutionFailureKind failureKind,
        ExecutionOutput? output = null,
        ErrorInfo? error = null,
        ExecutionRecoverability? recoverability = null)
    {
        TaskStatus previousStatus = Status;
        ExecutionTransitionSupport.EnsurePhase(
            subjectName: "Task",
            subjectId: TaskId.Value,
            currentPhase: ExecutionState.ExecutionPhase,
            ExecutionPhase.Running);

        ExecutionRecoverability effectiveRecoverability = ExecutionTransitionSupport.ResolveFailureRecoverability(
            failureKind,
            recoverability,
            invalidFailureKindMessage: "Failed tasks must use a transient, permanent, or unknown failure kind.",
            invalidRecoverabilityMessage: "Failed tasks must use a terminal recoverability value.");

        ExecutionState.ExecutionPhase = ExecutionPhase.Finished;
        ExecutionState.ExecutionOutcome = ExecutionOutcome.Failed;
        ExecutionState.FailureKind = failureKind;
        ExecutionState.Error = error;
        ExecutionState.Output = output;
        ExecutionState.Recoverability = effectiveRecoverability;
        NotifyTransition(previousStatus);
    }

    internal void MarkFailed(
        Exception exception,
        ExecutionFailureKind failureKind,
        ExecutionOutput? output = null,
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
