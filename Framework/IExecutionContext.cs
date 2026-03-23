namespace OrganizeMedia.Framework;

public interface IExecutionContext
{
    WorkflowSpecificationId WorkflowSpecificationId { get; }

    WorkflowInstanceId WorkflowInstanceId { get; }

    TaskSpecificationId TaskSpecificationId { get; }

    TaskInstanceId TaskInstanceId { get; }

    TaskInstanceId? SpawnedByTaskInstanceId { get; }

    TaskSpecification TaskSpecification { get; }

    IReadOnlyDictionary<TaskInstanceId, TaskStatus> DependencyStatuses { get; }

    IReadOnlyDictionary<TaskInstanceId, ExecutionOutput?> DependencyOutputs { get; }
}