namespace OrganizeMedia.Framework;

public record TaskFanInSpecification(
    TaskId JoinTaskId,
    IReadOnlyCollection<TaskId> AdditionalPrerequisiteTaskIds = null,
    bool IncludeSpawnedTasks = true)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(JoinTaskId.Value))
        {
            throw new InvalidOperationException("Fan-in specifications must reference a non-empty JoinTaskId.");
        }

        if (AdditionalPrerequisiteTaskIds is null)
            return;

        foreach (TaskId prerequisiteTaskId in AdditionalPrerequisiteTaskIds)
        {
            if (string.IsNullOrWhiteSpace(prerequisiteTaskId.Value))
            {
                throw new InvalidOperationException(
                    $"Fan-in specification for join task {JoinTaskId} contains an empty prerequisite task id.");
            }
        }
    }
}