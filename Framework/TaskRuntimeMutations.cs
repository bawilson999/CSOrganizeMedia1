namespace OrganizeMedia.Framework;

public sealed record TaskRuntimeMutations(
    IReadOnlyCollection<TaskSpecification> SpawnedTasks,
    IReadOnlyCollection<TaskDependencySpecification> AddedDependencies,
    IReadOnlyCollection<TaskFanInSpecification> FanInSpecifications)
{
    public static TaskRuntimeMutations None { get; } = new TaskRuntimeMutations(
        SpawnedTasks: Array.Empty<TaskSpecification>(),
        AddedDependencies: Array.Empty<TaskDependencySpecification>(),
        FanInSpecifications: Array.Empty<TaskFanInSpecification>());

    public static TaskRuntimeMutations Create(
        IReadOnlyCollection<TaskSpecification> spawnedTasks = null,
        IReadOnlyCollection<TaskDependencySpecification> addedDependencies = null,
        IReadOnlyCollection<TaskFanInSpecification> fanInSpecifications = null)
    {
        return new TaskRuntimeMutations(
            SpawnedTasks: spawnedTasks ?? Array.Empty<TaskSpecification>(),
            AddedDependencies: addedDependencies ?? Array.Empty<TaskDependencySpecification>(),
            FanInSpecifications: fanInSpecifications ?? Array.Empty<TaskFanInSpecification>());
    }
}