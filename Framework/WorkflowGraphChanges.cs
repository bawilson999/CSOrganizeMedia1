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
}