namespace OrganizeMedia.Framework;

public sealed record WorkflowGraphChanges(
    IReadOnlyCollection<TaskSpecification> SpawnedTasks,
    IReadOnlyCollection<TaskDependencySpecification> AddedDependencies,
    IReadOnlyCollection<TaskSpecificationSpawn> SpawnedTaskSpecifications,
    IReadOnlyCollection<TaskInstanceDependency> AddedInstanceDependencies)
{
    public static WorkflowGraphChanges None { get; } = new WorkflowGraphChanges(
        SpawnedTasks: Array.Empty<TaskSpecification>(),
        AddedDependencies: Array.Empty<TaskDependencySpecification>(),
        SpawnedTaskSpecifications: Array.Empty<TaskSpecificationSpawn>(),
        AddedInstanceDependencies: Array.Empty<TaskInstanceDependency>());

    public static WorkflowGraphChanges Create(
        IReadOnlyCollection<TaskSpecification>? spawnedTasks = null,
        IReadOnlyCollection<TaskDependencySpecification>? addedDependencies = null,
        IReadOnlyCollection<TaskSpecificationSpawn>? spawnedTaskSpecifications = null,
        IReadOnlyCollection<TaskInstanceDependency>? addedInstanceDependencies = null)
    {
        if (IsNullOrEmpty(spawnedTasks) &&
            IsNullOrEmpty(addedDependencies) &&
            IsNullOrEmpty(spawnedTaskSpecifications) &&
            IsNullOrEmpty(addedInstanceDependencies))
        {
            return None;
        }

        return new WorkflowGraphChanges(
            SpawnedTasks: spawnedTasks ?? Array.Empty<TaskSpecification>(),
            AddedDependencies: addedDependencies ?? Array.Empty<TaskDependencySpecification>(),
            SpawnedTaskSpecifications: spawnedTaskSpecifications ?? Array.Empty<TaskSpecificationSpawn>(),
            AddedInstanceDependencies: addedInstanceDependencies ?? Array.Empty<TaskInstanceDependency>());
    }

    private static bool IsNullOrEmpty<T>(IReadOnlyCollection<T>? values)
    {
        return values is null || values.Count == 0;
    }
}