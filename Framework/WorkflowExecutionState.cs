namespace OrganizeMedia.Framework;

internal class WorkflowExecutionState
{
    private Dictionary<TaskId, TaskExecutionState> _taskExecutionStates;
    private ExecutionPhase _executionPhase;
    private ExecutionOutcome _executionOutcome;
    private ExecutionFailureKind _failureKind;

    internal WorkflowExecutionState(WorkflowId workflowId)
    {
        DateTime createdTimestamp = DateTime.UtcNow;

        WorkflowId = workflowId;
        _taskExecutionStates = new Dictionary<TaskId, TaskExecutionState>();
        _executionPhase = ExecutionPhase.NotStarted;
        ExecutionOutcome = ExecutionOutcome.Pending;
        FailureKind = ExecutionFailureKind.None;
        Recoverability = ExecutionRecoverabilityDefaults.From(_executionPhase, ExecutionOutcome, FailureKind);
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

    public ErrorInfo Error { get; internal set; }

    public ExecutionOutput Output { get; internal set; }

    public DateTime? Timestamp { get; internal set; }

    public DateTime? CreatedTimestamp { get; internal set; }

    public DateTime? QueuedTimestamp { get; internal set; }

    public DateTime? ReadyToRunTimestamp { get; internal set; }

    public DateTime? RunningTimestamp { get; internal set; }

    public DateTime? FinishedTimestamp { get; internal set; }

    internal void AddTaskExecutionState(TaskExecutionState taskExecutionState)
    {
        _taskExecutionStates.Add(taskExecutionState.TaskId, taskExecutionState);
    }

    internal void TryGetTaskExecutionState(TaskId taskId, out TaskExecutionState taskExecutionState)
    {
        _taskExecutionStates.TryGetValue(taskId, out taskExecutionState);
    }

    internal WorkflowStatus ToStatus()
    {
        return new WorkflowStatus(
            WorkflowId: WorkflowId,
            ExecutionPhase: ExecutionPhase,
            ExecutionOutcome: ExecutionOutcome,
            FailureKind: FailureKind,
            Recoverability: Recoverability,
            TaskStatuses: _taskExecutionStates.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToStatus()),
            Error: Error,
            Output: Output,
            Timestamp: Timestamp,
            CreatedTimestamp: CreatedTimestamp,
            QueuedTimestamp: QueuedTimestamp,
            ReadyToRunTimestamp: ReadyToRunTimestamp,
            RunningTimestamp: RunningTimestamp,
            FinishedTimestamp: FinishedTimestamp);
    }
}