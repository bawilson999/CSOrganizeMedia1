namespace OrganizeMedia.Framework;

internal sealed class TaskExecutionState : ExecutionStateCore
{
    internal TaskExecutionState(
        WorkflowInstanceId workflowInstanceId,
        TaskSpecificationId taskSpecificationId,
        TaskInstanceId taskInstanceId,
        TaskInstanceId? spawnedByTaskInstanceId = null)
        : base(workflowInstanceId)
    {
        TaskSpecificationId = taskSpecificationId;
        TaskInstanceId = taskInstanceId;
        SpawnedByTaskInstanceId = spawnedByTaskInstanceId;
        TotalSteps = 1;
        CompletedSteps = 0;
    }

    public TaskSpecificationId TaskSpecificationId { get; init; }

    public TaskInstanceId TaskInstanceId { get; init; }

    public TaskInstanceId? SpawnedByTaskInstanceId { get; init; }

    public int TotalSteps { get; internal set; }

    public int CompletedSteps { get; internal set; }

    internal TaskStatus ToStatus()
    {
        return new TaskStatus(
            WorkflowSpecificationId: WorkflowSpecificationId,
            WorkflowInstanceId: WorkflowInstanceId,
            TaskSpecificationId: TaskSpecificationId,
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
            FinishedTimestamp: FinishedTimestamp,
            SpawnedByTaskInstanceId: SpawnedByTaskInstanceId);
    }
}