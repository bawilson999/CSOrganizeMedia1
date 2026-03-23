namespace OrganizeMedia.Framework;

internal class ExecutionContext : IExecutionContext
{
    private readonly IReadOnlyDictionary<TaskInstanceId, TaskStatus> _dependencyStatuses;

    internal ExecutionContext(Workflow workflow, Task task)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(task);

        WorkflowId = workflow.WorkflowId;
        TaskId = task.TaskId;
        TaskInstanceId = task.TaskInstanceId;
        TaskSpecification = task.Specification;

        Dictionary<TaskInstanceId, TaskStatus> dependencyStatuses = workflow
            .GetDependencies(task)
            .ToDictionary(dependency => dependency.TaskInstanceId, dependency => dependency.Status);

        _dependencyStatuses = dependencyStatuses;
    }

    public WorkflowId WorkflowId { get; }

    public TaskId TaskId { get; }

    public TaskInstanceId TaskInstanceId { get; }

    public TaskSpecification TaskSpecification { get; }

    public IReadOnlyDictionary<TaskInstanceId, TaskStatus> DependencyStatuses => _dependencyStatuses;

    public IReadOnlyDictionary<TaskInstanceId, ExecutionOutput?> DependencyOutputs => _dependencyStatuses.ToDictionary(
        pair => pair.Key,
        pair => pair.Value.Output);
}