namespace OrganizeMedia.Framework;

public record WorkflowSpecification(
    WorkflowTemplateId WorkflowTemplateId,
    IReadOnlyCollection<TaskSpecification> Tasks,
    IReadOnlyCollection<TaskDependencySpecification> Dependencies,
    int? MaxConcurrency = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(WorkflowTemplateId.Value))
        {
            throw new InvalidOperationException("Workflow specifications must have a non-empty WorkflowTemplateId.");
        }

        if (Tasks is null)
        {
            throw new InvalidOperationException($"Workflow specification {WorkflowTemplateId} must provide a task collection.");
        }

        if (Dependencies is null)
        {
            throw new InvalidOperationException($"Workflow specification {WorkflowTemplateId} must provide a dependency collection.");
        }

        if (MaxConcurrency is not null && MaxConcurrency <= 0)
        {
            throw new InvalidOperationException(
                $"Workflow specification {WorkflowTemplateId} must use a positive MaxConcurrency when provided.");
        }

        HashSet<TaskTemplateId> taskTemplateIds = new HashSet<TaskTemplateId>();
        Dictionary<TaskTemplateId, HashSet<TaskTemplateId>> adjacencyByTaskTemplateId = new Dictionary<TaskTemplateId, HashSet<TaskTemplateId>>();
        Dictionary<TaskTemplateId, int> incomingEdgeCountByTaskTemplateId = new Dictionary<TaskTemplateId, int>();

        foreach (TaskSpecification taskSpecification in Tasks)
        {
            if (taskSpecification is null)
            {
                throw new InvalidOperationException($"Workflow specification {WorkflowTemplateId} cannot contain null task specifications.");
            }

            taskSpecification.Validate();

            if (!taskTemplateIds.Add(taskSpecification.TaskTemplateId))
            {
                throw new InvalidOperationException(
                    $"Workflow specification {WorkflowTemplateId} contains duplicate task template id {taskSpecification.TaskTemplateId}.");
            }

            adjacencyByTaskTemplateId[taskSpecification.TaskTemplateId] = new HashSet<TaskTemplateId>();
            incomingEdgeCountByTaskTemplateId[taskSpecification.TaskTemplateId] = 0;
        }

        foreach (TaskDependencySpecification dependency in Dependencies)
        {
            if (dependency.PrerequisiteTaskTemplateId == dependency.DependentTaskTemplateId)
            {
                throw new InvalidOperationException(
                    $"Workflow specification {WorkflowTemplateId} contains a self-dependency on task template {dependency.PrerequisiteTaskTemplateId}.");
            }

            if (!taskTemplateIds.Contains(dependency.PrerequisiteTaskTemplateId))
            {
                throw new InvalidOperationException(
                    $"Workflow specification {WorkflowTemplateId} references missing prerequisite task template {dependency.PrerequisiteTaskTemplateId}.");
            }

            if (!taskTemplateIds.Contains(dependency.DependentTaskTemplateId))
            {
                throw new InvalidOperationException(
                    $"Workflow specification {WorkflowTemplateId} references missing dependent task template {dependency.DependentTaskTemplateId}.");
            }

            if (!adjacencyByTaskTemplateId[dependency.PrerequisiteTaskTemplateId].Add(dependency.DependentTaskTemplateId))
            {
                throw new InvalidOperationException(
                    $"Workflow specification {WorkflowTemplateId} contains duplicate dependency {dependency.PrerequisiteTaskTemplateId} -> {dependency.DependentTaskTemplateId}.");
            }

            incomingEdgeCountByTaskTemplateId[dependency.DependentTaskTemplateId] += 1;
        }

        Queue<TaskTemplateId> readyQueue = new Queue<TaskTemplateId>(
            incomingEdgeCountByTaskTemplateId
                .Where(pair => pair.Value == 0)
                .Select(pair => pair.Key));

        int visitedTaskCount = 0;

        while (readyQueue.Count > 0)
        {
            TaskTemplateId taskTemplateId = readyQueue.Dequeue();
            visitedTaskCount++;

            foreach (TaskTemplateId adjacentTaskTemplateId in adjacencyByTaskTemplateId[taskTemplateId])
            {
                incomingEdgeCountByTaskTemplateId[adjacentTaskTemplateId] -= 1;

                if (incomingEdgeCountByTaskTemplateId[adjacentTaskTemplateId] == 0)
                {
                    readyQueue.Enqueue(adjacentTaskTemplateId);
                }
            }
        }

        if (visitedTaskCount != taskTemplateIds.Count)
        {
            throw new InvalidOperationException(
                $"Workflow specification {WorkflowTemplateId} contains a dependency cycle.");
        }
    }
}
