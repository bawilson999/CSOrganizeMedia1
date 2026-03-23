namespace OrganizeMedia.Framework;

public interface IExecutionContext
{
    WorkflowId WorkflowId { get; }

    TaskId TaskId { get; }

    TaskInstanceId TaskInstanceId { get; }

    TaskSpecification TaskSpecification { get; }

    IReadOnlyDictionary<TaskInstanceId, TaskStatus> DependencyStatuses { get; }

    IReadOnlyDictionary<TaskInstanceId, ExecutionOutput?> DependencyOutputs { get; }
}