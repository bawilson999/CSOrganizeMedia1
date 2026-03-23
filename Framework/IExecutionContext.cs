namespace OrganizeMedia.Framework;

public interface IExecutionContext
{
    WorkflowId WorkflowId { get; }

    TaskId TaskId { get; }

    TaskSpecification TaskSpecification { get; }

    IReadOnlyDictionary<TaskId, TaskStatus> DependencyStatuses { get; }

    IReadOnlyDictionary<TaskId, ExecutionOutput?> DependencyOutputs { get; }
}