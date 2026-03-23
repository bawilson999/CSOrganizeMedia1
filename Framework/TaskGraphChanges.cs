namespace OrganizeMedia.Framework;

public sealed record TaskGraphChanges(
    IReadOnlyCollection<TaskSpecification> SpawnedTasks,
    IReadOnlyCollection<TaskDependencySpecification> AddedDependencies,
    IReadOnlyCollection<TaskTemplateSpawn> SpawnedTaskTemplates,
    IReadOnlyCollection<TaskInstanceDependency> AddedInstanceDependencies)
{
    public static TaskGraphChanges None { get; } = new TaskGraphChanges(
        SpawnedTasks: Array.Empty<TaskSpecification>(),
        AddedDependencies: Array.Empty<TaskDependencySpecification>(),
        SpawnedTaskTemplates: Array.Empty<TaskTemplateSpawn>(),
        AddedInstanceDependencies: Array.Empty<TaskInstanceDependency>());
}