namespace OrganizeMedia.Framework;

public record TaskSpecification(
    TaskId TaskId,
    string TaskType,
    string InputType = null,
    string InputJson = null,
    TaskId? SpawnedByTaskId = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TaskId.Value))
        {
            throw new InvalidOperationException("Task specifications must have a non-empty TaskId.");
        }

        if (string.IsNullOrWhiteSpace(TaskType))
        {
            throw new InvalidOperationException($"Task specification {TaskId} must have a non-empty TaskType.");
        }

        if (!string.IsNullOrWhiteSpace(InputJson) && string.IsNullOrWhiteSpace(InputType))
        {
            throw new InvalidOperationException(
                $"Task specification {TaskId} must provide InputType when InputJson is present.");
        }
    }
}
