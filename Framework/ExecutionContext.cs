namespace OrganizeMedia.Framework;

internal class ExecutionContext : IExecutionContext
{
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

        DependencyStatuses = dependencyStatuses;
        DependencyOutputs = dependencyStatuses.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Output);
    }

    public WorkflowId WorkflowId { get; }

    public TaskId TaskId { get; }

    public TaskSpecification TaskSpecification { get; }

    public IReadOnlyDictionary<TaskId, TaskStatus> DependencyStatuses { get; }

    public IReadOnlyDictionary<TaskId, ExecutionOutput> DependencyOutputs { get; }
}