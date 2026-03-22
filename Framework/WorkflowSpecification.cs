namespace OrganizeMedia.Framework;

public record WorkflowSpecification(
    WorkflowId WorkflowId,
    IReadOnlyCollection<TaskSpecification> Tasks,
    IReadOnlyCollection<TaskDependencySpecification> Dependencies,
    int? MaxConcurrency = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(WorkflowId.Value))
        {
            throw new InvalidOperationException("Workflow specifications must have a non-empty WorkflowId.");
        }

        if (Tasks is null)
        {
            throw new InvalidOperationException($"Workflow specification {WorkflowId} must provide a task collection.");
        }

        if (Dependencies is null)
        {
            throw new InvalidOperationException($"Workflow specification {WorkflowId} must provide a dependency collection.");
        }

        if (MaxConcurrency is not null && MaxConcurrency <= 0)
        {
            throw new InvalidOperationException(
                $"Workflow specification {WorkflowId} must use a positive MaxConcurrency when provided.");
        }

        HashSet<TaskId> taskIds = new HashSet<TaskId>();
        Dictionary<TaskId, HashSet<TaskId>> adjacencyByTaskId = new Dictionary<TaskId, HashSet<TaskId>>();
        Dictionary<TaskId, int> incomingEdgeCountByTaskId = new Dictionary<TaskId, int>();

        foreach (TaskSpecification taskSpecification in Tasks)
        {
            if (taskSpecification is null)
            {
                throw new InvalidOperationException($"Workflow specification {WorkflowId} cannot contain null task specifications.");
            }

            taskSpecification.Validate();

            if (!taskIds.Add(taskSpecification.TaskId))
            {
                throw new InvalidOperationException(
                    $"Workflow specification {WorkflowId} contains duplicate task id {taskSpecification.TaskId}.");
            }

            adjacencyByTaskId[taskSpecification.TaskId] = new HashSet<TaskId>();
            incomingEdgeCountByTaskId[taskSpecification.TaskId] = 0;
        }

        foreach (TaskDependencySpecification dependency in Dependencies)
        {
            if (dependency.PrerequisiteTaskId == dependency.DependentTaskId)
            {
                throw new InvalidOperationException(
                    $"Workflow specification {WorkflowId} contains a self-dependency on task {dependency.PrerequisiteTaskId}.");
            }

            if (!taskIds.Contains(dependency.PrerequisiteTaskId))
            {
                throw new InvalidOperationException(
                    $"Workflow specification {WorkflowId} references missing prerequisite task {dependency.PrerequisiteTaskId}.");
            }

            if (!taskIds.Contains(dependency.DependentTaskId))
            {
                throw new InvalidOperationException(
                    $"Workflow specification {WorkflowId} references missing dependent task {dependency.DependentTaskId}.");
            }

            if (!adjacencyByTaskId[dependency.PrerequisiteTaskId].Add(dependency.DependentTaskId))
            {
                throw new InvalidOperationException(
                    $"Workflow specification {WorkflowId} contains duplicate dependency {dependency.PrerequisiteTaskId} -> {dependency.DependentTaskId}.");
            }

            incomingEdgeCountByTaskId[dependency.DependentTaskId] += 1;
        }

        Queue<TaskId> readyQueue = new Queue<TaskId>(
            incomingEdgeCountByTaskId
                .Where(pair => pair.Value == 0)
                .Select(pair => pair.Key));

        int visitedTaskCount = 0;

        while (readyQueue.Count > 0)
        {
            TaskId taskId = readyQueue.Dequeue();
            visitedTaskCount++;

            foreach (TaskId adjacentTaskId in adjacencyByTaskId[taskId])
            {
                incomingEdgeCountByTaskId[adjacentTaskId] -= 1;

                if (incomingEdgeCountByTaskId[adjacentTaskId] == 0)
                {
                    readyQueue.Enqueue(adjacentTaskId);
                }
            }
        }

        if (visitedTaskCount != taskIds.Count)
        {
            throw new InvalidOperationException(
                $"Workflow specification {WorkflowId} contains a dependency cycle.");
        }
    }
}
