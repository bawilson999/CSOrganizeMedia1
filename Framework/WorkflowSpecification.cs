namespace OrganizeMedia.Framework;

public record WorkflowSpecification(
    WorkflowSpecificationId WorkflowSpecificationId,
    IReadOnlyCollection<TaskSpecification> Tasks,
    IReadOnlyCollection<TaskDependencySpecification> Dependencies,
    int? MaxConcurrency = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(WorkflowSpecificationId.Value))
        {
            throw new InvalidOperationException("Workflow specifications must have a non-empty WorkflowSpecificationId.");
        }

        if (Tasks is null)
        {
            throw new InvalidOperationException($"Workflow specification {WorkflowSpecificationId} must provide a task collection.");
        }

        if (Dependencies is null)
        {
            throw new InvalidOperationException($"Workflow specification {WorkflowSpecificationId} must provide a dependency collection.");
        }

        if (MaxConcurrency is not null && MaxConcurrency <= 0)
        {
            throw new InvalidOperationException(
                $"Workflow specification {WorkflowSpecificationId} must use a positive MaxConcurrency when provided.");
        }

        HashSet<TaskSpecificationId> taskSpecificationIds = new HashSet<TaskSpecificationId>();
        Dictionary<TaskSpecificationId, HashSet<TaskSpecificationId>> adjacencyByTaskSpecificationId = new Dictionary<TaskSpecificationId, HashSet<TaskSpecificationId>>();
        Dictionary<TaskSpecificationId, int> incomingEdgeCountByTaskSpecificationId = new Dictionary<TaskSpecificationId, int>();

        foreach (TaskSpecification taskSpecification in Tasks)
        {
            if (taskSpecification is null)
            {
                throw new InvalidOperationException($"Workflow specification {WorkflowSpecificationId} cannot contain null task specifications.");
            }

            taskSpecification.Validate();

            if (!taskSpecificationIds.Add(taskSpecification.TaskSpecificationId))
            {
                throw new InvalidOperationException(
                    $"Workflow specification {WorkflowSpecificationId} contains duplicate task specification id {taskSpecification.TaskSpecificationId}.");
            }

            adjacencyByTaskSpecificationId[taskSpecification.TaskSpecificationId] = new HashSet<TaskSpecificationId>();
            incomingEdgeCountByTaskSpecificationId[taskSpecification.TaskSpecificationId] = 0;
        }

        foreach (TaskDependencySpecification dependency in Dependencies)
        {
            if (dependency.PrerequisiteTaskSpecificationId == dependency.DependentTaskSpecificationId)
            {
                throw new InvalidOperationException(
                        $"Workflow specification {WorkflowSpecificationId} contains a self-dependency on task specification {dependency.PrerequisiteTaskSpecificationId}.");
            }

            if (!taskSpecificationIds.Contains(dependency.PrerequisiteTaskSpecificationId))
            {
                throw new InvalidOperationException(
                        $"Workflow specification {WorkflowSpecificationId} references missing prerequisite task specification {dependency.PrerequisiteTaskSpecificationId}.");
            }

            if (!taskSpecificationIds.Contains(dependency.DependentTaskSpecificationId))
            {
                throw new InvalidOperationException(
                        $"Workflow specification {WorkflowSpecificationId} references missing dependent task specification {dependency.DependentTaskSpecificationId}.");
            }

            if (!adjacencyByTaskSpecificationId[dependency.PrerequisiteTaskSpecificationId].Add(dependency.DependentTaskSpecificationId))
            {
                throw new InvalidOperationException(
                    $"Workflow specification {WorkflowSpecificationId} contains duplicate dependency {dependency.PrerequisiteTaskSpecificationId} -> {dependency.DependentTaskSpecificationId}.");
            }

            incomingEdgeCountByTaskSpecificationId[dependency.DependentTaskSpecificationId] += 1;
        }

        Queue<TaskSpecificationId> readyQueue = new Queue<TaskSpecificationId>(
            incomingEdgeCountByTaskSpecificationId
                .Where(pair => pair.Value == 0)
                .Select(pair => pair.Key));

        int visitedTaskCount = 0;

        while (readyQueue.Count > 0)
        {
            TaskSpecificationId taskSpecificationId = readyQueue.Dequeue();
            visitedTaskCount++;

            foreach (TaskSpecificationId adjacentTaskSpecificationId in adjacencyByTaskSpecificationId[taskSpecificationId])
            {
                incomingEdgeCountByTaskSpecificationId[adjacentTaskSpecificationId] -= 1;

                if (incomingEdgeCountByTaskSpecificationId[adjacentTaskSpecificationId] == 0)
                {
                    readyQueue.Enqueue(adjacentTaskSpecificationId);
                }
            }
        }

        if (visitedTaskCount != taskSpecificationIds.Count)
        {
            throw new InvalidOperationException(
                $"Workflow specification {WorkflowSpecificationId} contains a dependency cycle.");
        }
    }
}
