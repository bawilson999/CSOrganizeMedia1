namespace OrganizeMedia.Framework;

public sealed record TaskGraphChanges(
    IReadOnlyCollection<TaskSpecification> SpawnedTasks,
    IReadOnlyCollection<TaskDependencySpecification> AddedDependencies,
    IReadOnlyCollection<TaskSpecificationSpawn> SpawnedTaskSpecifications,
    IReadOnlyCollection<TaskInstanceDependency> AddedInstanceDependencies)
{
    public static TaskGraphChanges None { get; } = new TaskGraphChanges(
        SpawnedTasks: Array.Empty<TaskSpecification>(),
        AddedDependencies: Array.Empty<TaskDependencySpecification>(),
        SpawnedTaskSpecifications: Array.Empty<TaskSpecificationSpawn>(),
        AddedInstanceDependencies: Array.Empty<TaskInstanceDependency>());
}