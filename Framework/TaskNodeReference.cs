namespace OrganizeMedia.Framework;

public sealed record TaskNodeReference
{
    private TaskNodeReference(TaskSpecificationId? taskSpecificationId, TaskInstanceId? taskInstanceId, string? spawnReference)
    {
        int populatedReferenceCount =
            (taskSpecificationId is not null ? 1 : 0) +
            (taskInstanceId is not null ? 1 : 0) +
            (string.IsNullOrWhiteSpace(spawnReference) ? 0 : 1);

        if (populatedReferenceCount != 1)
        {
            throw new InvalidOperationException("Task node references must specify exactly one reference kind.");
        }

        TaskSpecificationId = taskSpecificationId;
        TaskInstanceId = taskInstanceId;
        SpawnReference = spawnReference;
    }

    public TaskSpecificationId? TaskSpecificationId { get; }

    public TaskInstanceId? TaskInstanceId { get; }

    public string? SpawnReference { get; }

    public static TaskNodeReference ForTaskSpecification(TaskSpecificationId taskSpecificationId)
    {
        return new TaskNodeReference(taskSpecificationId, null, null);
    }

    public static TaskNodeReference ForTaskInstance(TaskInstanceId taskInstanceId)
    {
        return new TaskNodeReference(null, taskInstanceId, null);
    }

    public static TaskNodeReference ForSpawnReference(string spawnReference)
    {
        return new TaskNodeReference(null, null, spawnReference);
    }
}