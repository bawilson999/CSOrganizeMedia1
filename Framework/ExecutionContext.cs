namespace OrganizeMedia.Framework;

internal class ExecutionContext : IExecutionContext
{
    private readonly IReadOnlyDictionary<TaskId, TaskStatus> _dependencyStatuses;

    internal ExecutionContext(Workflow workflow, Task task)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(task);

        WorkflowId = workflow.WorkflowId;
        TaskId = task.TaskId;
        TaskSpecification = task.Specification;

        Dictionary<TaskId, TaskStatus> dependencyStatuses = workflow
            .GetDependencies(task)
            .ToDictionary(dependency => dependency.TaskId, dependency => dependency.Status);

        _dependencyStatuses = dependencyStatuses;
    }

    public WorkflowId WorkflowId { get; }

    public TaskId TaskId { get; }

    public TaskSpecification TaskSpecification { get; }

    public IReadOnlyDictionary<TaskId, TaskStatus> DependencyStatuses => _dependencyStatuses;

    public IReadOnlyDictionary<TaskId, ExecutionOutput> DependencyOutputs => _dependencyStatuses.ToDictionary(
        pair => pair.Key,
        pair => pair.Value.Output);
}