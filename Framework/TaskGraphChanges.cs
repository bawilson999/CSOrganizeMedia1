namespace OrganizeMedia.Framework;

public sealed record TaskGraphChanges(
    IReadOnlyCollection<TaskSpecification> SpawnedTasks,
    IReadOnlyCollection<TaskDependencySpecification> AddedDependencies)
{
    public static TaskGraphChanges None { get; } = new TaskGraphChanges(
        SpawnedTasks: Array.Empty<TaskSpecification>(),
        AddedDependencies: Array.Empty<TaskDependencySpecification>());
}