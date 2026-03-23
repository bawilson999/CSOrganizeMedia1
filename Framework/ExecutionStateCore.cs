namespace OrganizeMedia.Framework;

internal abstract class ExecutionStateCore
{
    private ExecutionPhase _executionPhase;
    private ExecutionOutcome _executionOutcome;
    private ExecutionFailureKind _failureKind;

    protected ExecutionStateCore(WorkflowId workflowId)
    {
        DateTime createdTimestamp = DateTime.UtcNow;

        WorkflowId = workflowId;
        _executionPhase = ExecutionPhase.NotStarted;
        _executionOutcome = ExecutionOutcome.Pending;
        _failureKind = ExecutionFailureKind.None;
        Recoverability = ExecutionRecoverabilityDefaults.From(_executionPhase, _executionOutcome, _failureKind);
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
            {
                _failureKind = ExecutionFailureKind.None;
            }

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

    public ErrorInfo Error { get; internal set; }

    public ExecutionOutput Output { get; internal set; }

    public DateTime? Timestamp { get; internal set; }

    public DateTime? CreatedTimestamp { get; internal set; }

    public DateTime? QueuedTimestamp { get; internal set; }

    public DateTime? ReadyToRunTimestamp { get; internal set; }

    public DateTime? RunningTimestamp { get; internal set; }

    public DateTime? FinishedTimestamp { get; internal set; }
}