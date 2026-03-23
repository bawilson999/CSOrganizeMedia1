namespace OrganizeMedia.Framework;

internal sealed class WorkflowExecutionState : ExecutionStateCore
{
    private Dictionary<TaskId, TaskExecutionState> _taskExecutionStates;

    internal WorkflowExecutionState(WorkflowId workflowId)
        : base(workflowId)
    {
        _taskExecutionStates = new Dictionary<TaskId, TaskExecutionState>();
    }

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