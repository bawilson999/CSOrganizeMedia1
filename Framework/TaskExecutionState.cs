namespace OrganizeMedia.Framework;

internal sealed class TaskExecutionState : ExecutionStateCore
{
    internal TaskExecutionState(
        WorkflowInstanceId workflowInstanceId,
        TaskTemplateId taskTemplateId,
        TaskInstanceId taskInstanceId,
        TaskInstanceId? spawnedByTaskInstanceId = null)
        : base(workflowInstanceId)
    {
        TaskTemplateId = taskTemplateId;
        TaskInstanceId = taskInstanceId;
        SpawnedByTaskInstanceId = spawnedByTaskInstanceId;
        TotalSteps = 1;
        CompletedSteps = 0;
    }

    public TaskTemplateId TaskTemplateId { get; init; }

    public TaskInstanceId TaskInstanceId { get; init; }

    public TaskInstanceId? SpawnedByTaskInstanceId { get; init; }

    public int TotalSteps { get; internal set; }

    public int CompletedSteps { get; internal set; }

    internal TaskStatus ToStatus()
    {
        return new TaskStatus(
            WorkflowTemplateId: WorkflowTemplateId,
            WorkflowInstanceId: WorkflowInstanceId,
            TaskTemplateId: TaskTemplateId,
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