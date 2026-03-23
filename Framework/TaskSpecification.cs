namespace OrganizeMedia.Framework;

public record TaskSpecification(
    TaskId TaskId,
    TaskType TaskType,
    InputType? InputType = null,
    string? InputJson = null,
    TaskId? SpawnedByTaskId = null,
    int InitialInstanceCount = 1)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TaskId.Value))
        {
            throw new InvalidOperationException("Task specifications must have a non-empty TaskId.");
        }

        if (string.IsNullOrWhiteSpace(TaskType.Value))
        {
            throw new InvalidOperationException($"Task specification {TaskId} must have a non-empty TaskType.");
        }

        if (!string.IsNullOrWhiteSpace(InputJson) &&
            (!InputType.HasValue || string.IsNullOrWhiteSpace(InputType.Value.Value)))
        {
            throw new InvalidOperationException(
                $"Task specification {TaskId} must provide InputType when InputJson is present.");
        }

        if (InitialInstanceCount < 0)
        {
            throw new InvalidOperationException(
                $"Task specification {TaskId} must use a non-negative InitialInstanceCount.");
        }
    }
}
