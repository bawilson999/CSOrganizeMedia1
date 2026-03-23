namespace OrganizeMedia.Framework;

public sealed record TaskNodeReference
{
    private TaskNodeReference(TaskSpecificationId? taskSpecificationId, TaskInstanceId? taskInstanceId, string? spawnKey)
    {
        int populatedReferenceCount =
            (taskSpecificationId is not null ? 1 : 0) +
            (taskInstanceId is not null ? 1 : 0) +
            (string.IsNullOrWhiteSpace(spawnKey) ? 0 : 1);

        if (populatedReferenceCount != 1)
        {
            throw new InvalidOperationException("Task node references must specify exactly one reference kind.");
        }

        TaskSpecificationId = taskSpecificationId;
        TaskInstanceId = taskInstanceId;
        SpawnKey = spawnKey;
    }

    public TaskSpecificationId? TaskSpecificationId { get; }

    public TaskInstanceId? TaskInstanceId { get; }

    public string? SpawnKey { get; }

    public static TaskNodeReference SpecificationTask(TaskSpecificationId taskSpecificationId)
    {
        return new TaskNodeReference(taskSpecificationId, null, null);
    }

    public static TaskNodeReference TaskInstance(TaskInstanceId taskInstanceId)
    {
        return new TaskNodeReference(null, taskInstanceId, null);
    }

    public static TaskNodeReference SpawnedTask(string spawnKey)
    {
        return new TaskNodeReference(null, null, spawnKey);
    }
}