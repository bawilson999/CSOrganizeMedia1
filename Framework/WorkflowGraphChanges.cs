namespace OrganizeMedia.Framework;

public sealed record WorkflowGraphChanges(
    IReadOnlyCollection<TaskSpecification> SpawnedTasks,
    IReadOnlyCollection<TaskDependencySpecification> AddedDependencies,
    IReadOnlyCollection<TaskSpawnRequest> TaskSpawnRequests,
    IReadOnlyCollection<TaskInstanceDependency> AddedInstanceDependencies)
{
    public static WorkflowGraphChanges None { get; } = new WorkflowGraphChanges(
        SpawnedTasks: Array.Empty<TaskSpecification>(),
        AddedDependencies: Array.Empty<TaskDependencySpecification>(),
        TaskSpawnRequests: Array.Empty<TaskSpawnRequest>(),
        AddedInstanceDependencies: Array.Empty<TaskInstanceDependency>());

    public static WorkflowGraphChanges Create(
        IReadOnlyCollection<TaskSpecification>? spawnedTasks = null,
        IReadOnlyCollection<TaskDependencySpecification>? addedDependencies = null,
        IReadOnlyCollection<TaskSpawnRequest>? taskSpawnRequests = null,
        IReadOnlyCollection<TaskInstanceDependency>? addedInstanceDependencies = null)
    {
        if (IsNullOrEmpty(spawnedTasks) &&
            IsNullOrEmpty(addedDependencies) &&
            IsNullOrEmpty(taskSpawnRequests) &&
            IsNullOrEmpty(addedInstanceDependencies))
        {
            return None;
        }

        return new WorkflowGraphChanges(
            SpawnedTasks: spawnedTasks ?? Array.Empty<TaskSpecification>(),
            AddedDependencies: addedDependencies ?? Array.Empty<TaskDependencySpecification>(),
            TaskSpawnRequests: taskSpawnRequests ?? Array.Empty<TaskSpawnRequest>(),
            AddedInstanceDependencies: addedInstanceDependencies ?? Array.Empty<TaskInstanceDependency>());
    }

    private static bool IsNullOrEmpty<T>(IReadOnlyCollection<T>? values)
    {
        return values is null || values.Count == 0;
    }
}