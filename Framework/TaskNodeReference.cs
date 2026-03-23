namespace OrganizeMedia.Framework;

public sealed record TaskNodeReference
{
    private TaskNodeReference(TaskId? taskId, TaskInstanceId? taskInstanceId, string? spawnKey)
    {
        int populatedReferenceCount =
            (taskId is not null ? 1 : 0) +
            (taskInstanceId is not null ? 1 : 0) +
            (string.IsNullOrWhiteSpace(spawnKey) ? 0 : 1);

        if (populatedReferenceCount != 1)
        {
            throw new InvalidOperationException("Task node references must specify exactly one reference kind.");
        }

        TaskId = taskId;
        TaskInstanceId = taskInstanceId;
        SpawnKey = spawnKey;
    }

    public TaskId? TaskId { get; }

    public TaskInstanceId? TaskInstanceId { get; }

    public string? SpawnKey { get; }

    public static TaskNodeReference TemplateTask(TaskId taskId)
    {
        return new TaskNodeReference(taskId, null, null);
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