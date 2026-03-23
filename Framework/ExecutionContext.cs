namespace OrganizeMedia.Framework;

internal class ExecutionContext : IExecutionContext
{
    private readonly IReadOnlyDictionary<TaskInstanceId, TaskStatus> _dependencyStatuses;

    internal ExecutionContext(Workflow workflow, Task task)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(task);

        WorkflowTemplateId = workflow.WorkflowTemplateId;
        WorkflowInstanceId = workflow.WorkflowInstanceId;
        TaskTemplateId = task.TaskTemplateId;
        TaskInstanceId = task.TaskInstanceId;
        SpawnedByTaskInstanceId = task.SpawnedByTaskInstanceId;
        TaskSpecification = task.Specification;

        Dictionary<TaskInstanceId, TaskStatus> dependencyStatuses = workflow
            .GetDependencies(task)
            .ToDictionary(dependency => dependency.TaskInstanceId, dependency => dependency.Status);

        _dependencyStatuses = dependencyStatuses;
    }

    public WorkflowTemplateId WorkflowTemplateId { get; }

    public WorkflowInstanceId WorkflowInstanceId { get; }

    public TaskTemplateId TaskTemplateId { get; }

    public TaskInstanceId TaskInstanceId { get; }

    public TaskInstanceId? SpawnedByTaskInstanceId { get; }

    public TaskSpecification TaskSpecification { get; }

    public IReadOnlyDictionary<TaskInstanceId, TaskStatus> DependencyStatuses => _dependencyStatuses;

    public IReadOnlyDictionary<TaskInstanceId, ExecutionOutput?> DependencyOutputs => _dependencyStatuses.ToDictionary(
        pair => pair.Key,
        pair => pair.Value.Output);
}