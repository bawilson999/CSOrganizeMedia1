namespace OrganizeMedia.Framework;

internal sealed class TaskExecutionState : ExecutionStateCore
{
    internal TaskExecutionState(WorkflowId workflowId, TaskId taskId, TaskInstanceId taskInstanceId)
        : base(workflowId)
    {
        TaskId = taskId;
        TaskInstanceId = taskInstanceId;
        TotalSteps = 1;
        CompletedSteps = 0;
    }

    public TaskId TaskId { get; init; }

    public TaskInstanceId TaskInstanceId { get; init; }

    public int TotalSteps { get; internal set; }

    public int CompletedSteps { get; internal set; }

    internal TaskStatus ToStatus()
    {
        return new TaskStatus(
            WorkflowId: WorkflowId,
            TaskId: TaskId,
            TaskInstanceId: TaskInstanceId,
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