namespace OrganizeMedia.Framework;

public sealed record TaskRuntimeMutations(
    IReadOnlyCollection<TaskSpecification> SpawnedTasks,
    IReadOnlyCollection<TaskDependencySpecification> AddedDependencies)
{
    public static TaskRuntimeMutations None { get; } = new TaskRuntimeMutations(
        SpawnedTasks: Array.Empty<TaskSpecification>(),
        AddedDependencies: Array.Empty<TaskDependencySpecification>());
}