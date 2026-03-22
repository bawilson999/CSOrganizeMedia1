namespace OrganizeMedia.Framework;

internal class TaskExecutionState
{
    internal TaskExecutionState(WorkflowId workflowId, TaskId taskId)
    {
        DateTime createdTimestamp = DateTime.UtcNow;

        WorkflowId = workflowId;
        TaskId = taskId;
        _executionPhase = ExecutionPhase.NotStarted;
        ExecutionOutcome = ExecutionOutcome.Pending;
        FailureKind = ExecutionFailureKind.None;
        Recoverability = ExecutionRecoverabilityDefaults.From(_executionPhase, ExecutionOutcome, FailureKind);
        TotalSteps = 1;
        CompletedSteps = 0;
        Error = null;
        Output = null;
        Timestamp = createdTimestamp;
        CreatedTimestamp = createdTimestamp;
        QueuedTimestamp = null;
        ReadyToRunTimestamp = null;
        RunningTimestamp = null;
        FinishedTimestamp = null;
    }

    public WorkflowId WorkflowId { get; init; }

    public TaskId TaskId { get; init; }

    private ExecutionPhase _executionPhase;
    private ExecutionOutcome _executionOutcome;
    private ExecutionFailureKind _failureKind;

    public ExecutionPhase ExecutionPhase
    {
        get { return _executionPhase; }
        internal set
        {
            DateTime transitionTimestamp = DateTime.UtcNow;

            _executionPhase = value;
            Timestamp = transitionTimestamp;

            switch (_executionPhase)
            {
                case ExecutionPhase.Queued:
                    QueuedTimestamp = transitionTimestamp;
                    break;

                case ExecutionPhase.ReadyToRun:
                    ReadyToRunTimestamp = transitionTimestamp;
                    break;

                case ExecutionPhase.Running:
                    RunningTimestamp = transitionTimestamp;
                    break;

                case ExecutionPhase.Finished:
                    FinishedTimestamp = transitionTimestamp;
                    break;
            }

            Recoverability = ExecutionRecoverabilityDefaults.From(_executionPhase, _executionOutcome, _failureKind);
        }
    }

    public ExecutionOutcome ExecutionOutcome
    {
        get { return _executionOutcome; }
        internal set
        {
            _executionOutcome = value;

            if (_executionOutcome != ExecutionOutcome.Failed)
                _failureKind = ExecutionFailureKind.None;

            Recoverability = ExecutionRecoverabilityDefaults.From(_executionPhase, _executionOutcome, _failureKind);
        }
    }

    public ExecutionFailureKind FailureKind
    {
        get { return _failureKind; }
        internal set
        {
            _failureKind = value;
            Recoverability = ExecutionRecoverabilityDefaults.From(_executionPhase, _executionOutcome, _failureKind);
        }
    }

    public ExecutionRecoverability Recoverability { get; internal set; }

    public int TotalSteps { get; internal set; }

    public int CompletedSteps { get; internal set; }

    public ErrorInfo Error { get; internal set; }

    public ExecutionOutput Output { get; internal set; }

    public DateTime? Timestamp { get; internal set; }

    public DateTime? CreatedTimestamp { get; internal set; }

    public DateTime? QueuedTimestamp { get; internal set; }

    public DateTime? ReadyToRunTimestamp { get; internal set; }

    public DateTime? RunningTimestamp { get; internal set; }

    public DateTime? FinishedTimestamp { get; internal set; }

    internal TaskStatus ToStatus()
    {
        return new TaskStatus(
            WorkflowId: WorkflowId,
            TaskId: TaskId,
            ExecutionPhase: ExecutionPhase,
            ExecutionOutcome: ExecutionOutcome,
            FailureKind: FailureKind,
            Recoverability: Recoverability,
            Error: Error,
            Output: Output,
            TotalSteps: TotalSteps,
            CompletedSteps: CompletedSteps,
            Timestamp: Timestamp,
            CreatedTimestamp: CreatedTimestamp,
            QueuedTimestamp: QueuedTimestamp,
            ReadyToRunTimestamp: ReadyToRunTimestamp,
            RunningTimestamp: RunningTimestamp,
            FinishedTimestamp: FinishedTimestamp);
    }
}