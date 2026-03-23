namespace OrganizeMedia.Framework;

public class Task
{
    private IWorkflowObserver _observer;

    internal Task(
        WorkflowInstanceId workflowInstanceId,
        TaskSpecification specification,
        TaskInstanceId taskInstanceId,
        TaskInstanceId? spawnedByTaskInstanceId = null)
        : this(
            workflowInstanceId,
            specification,
            new TaskExecutionState(workflowInstanceId, specification.TaskSpecificationId, taskInstanceId, spawnedByTaskInstanceId))
    {
    }

    internal Task(WorkflowInstanceId workflowInstanceId, TaskSpecification specification, TaskExecutionState executionState)
    {
        ArgumentNullException.ThrowIfNull(specification);

        WorkflowInstanceId = workflowInstanceId;
        TaskSpecificationId = specification.TaskSpecificationId;
        TaskInstanceId = executionState.TaskInstanceId;
        SpawnedByTaskInstanceId = executionState.SpawnedByTaskInstanceId;
        Specification = specification;
        ExecutionState = executionState;
        _observer = NullWorkflowObserver.Instance;
    }

    public WorkflowSpecificationId WorkflowSpecificationId => WorkflowInstanceId.WorkflowSpecificationId;

    public WorkflowInstanceId WorkflowInstanceId { get; init; }

    public TaskSpecificationId TaskSpecificationId { get; init; }

    public TaskInstanceId TaskInstanceId { get; init; }

    public TaskInstanceId? SpawnedByTaskInstanceId { get; init; }

    public TaskSpecification Specification { get; init; }

    internal TaskExecutionState ExecutionState { get; init; }

    public TaskStatus Status => ExecutionState.ToStatus();

    public override string ToString()
    {
        return $"/{WorkflowInstanceId}/{TaskInstanceId}";
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
            subjectId: TaskSpecificationId.Value,
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
            subjectId: TaskSpecificationId.Value,
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
            subjectId: TaskSpecificationId.Value,
            currentPhase: ExecutionState.ExecutionPhase,
            ExecutionPhase.Queued);
        ExecutionState.ExecutionPhase = ExecutionPhase.Running;
        NotifyTransition(previousStatus);
    }

    private void Complete(
        TaskStatus previousStatus,
        ExecutionOutcome executionOutcome,
        ExecutionOutput? output,
        ErrorInfo? error,
        ExecutionFailureKind failureKind,
        ExecutionRecoverability? recoverability,
        bool completeAllSteps)
    {
        ExecutionState.ExecutionPhase = ExecutionPhase.Finished;
        ExecutionState.ExecutionOutcome = executionOutcome;

        if (executionOutcome == ExecutionOutcome.Failed)
        {
            ExecutionState.FailureKind = failureKind;
        }

        if (completeAllSteps)
        {
            ExecutionState.CompletedSteps = ExecutionState.TotalSteps;
        }

        ExecutionState.Error = error;
        ExecutionState.Output = output;

        if (recoverability is not null)
        {
            ExecutionState.Recoverability = recoverability.Value;
        }

        NotifyTransition(previousStatus);
    }

    internal void MarkSucceeded(ExecutionOutput? output = null)
    {
        TaskStatus previousStatus = Status;
        ExecutionTransitionSupport.EnsurePhase(
            subjectName: "Task",
            subjectId: TaskSpecificationId.Value,
            currentPhase: ExecutionState.ExecutionPhase,
            ExecutionPhase.Running);
        Complete(
            previousStatus,
            ExecutionOutcome.Succeeded,
            output,
            error: null,
            failureKind: ExecutionFailureKind.None,
            recoverability: null,
            completeAllSteps: true);
    }

    internal void MarkCanceled(
        ExecutionOutput? output = null,
        ErrorInfo? error = null,
        ExecutionRecoverability recoverability = ExecutionRecoverability.Retryable)
    {
        TaskStatus previousStatus = Status;
        ExecutionTransitionSupport.EnsurePhase(
            subjectName: "Task",
            subjectId: TaskSpecificationId.Value,
            currentPhase: ExecutionState.ExecutionPhase,
            ExecutionPhase.Running);
        ExecutionTransitionSupport.EnsureTerminalRecoverability(
            recoverability,
            nameof(recoverability),
            "Canceled tasks must use a terminal recoverability value.");

        Complete(
            previousStatus,
            ExecutionOutcome.Canceled,
            output,
            error,
            failureKind: ExecutionFailureKind.None,
            recoverability,
            completeAllSteps: false);
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
            subjectId: TaskSpecificationId.Value,
            currentPhase: ExecutionState.ExecutionPhase,
            ExecutionPhase.Running);

        ExecutionRecoverability effectiveRecoverability = ExecutionTransitionSupport.ResolveFailureRecoverability(
            failureKind,
            recoverability,
            invalidFailureKindMessage: "Failed tasks must use a transient, permanent, or unknown failure kind.",
            invalidRecoverabilityMessage: "Failed tasks must use a terminal recoverability value.");

        Complete(
            previousStatus,
            ExecutionOutcome.Failed,
            output,
            error,
            failureKind,
            effectiveRecoverability,
            completeAllSteps: false);
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
                $"Task {TaskSpecificationId} cannot be reset after execution has started.");
        }

        ExecutionState.ExecutionPhase = ExecutionPhase.NotStarted;
        ExecutionState.ExecutionOutcome = ExecutionOutcome.Pending;
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
                WorkflowSpecificationId: WorkflowSpecificationId,
                WorkflowInstanceId: WorkflowInstanceId,
                TaskSpecificationId: TaskSpecificationId,
                TaskInstanceId: TaskInstanceId,
                PreviousStatus: previousStatus,
                CurrentStatus: currentStatus,
                Timestamp: currentStatus.Timestamp ?? DateTime.UtcNow));
        }
        catch
        {
        }
    }
}
