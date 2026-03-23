namespace OrganizeMedia.Framework;

public interface IExecutionContext
{
    WorkflowTemplateId WorkflowTemplateId { get; }

    WorkflowInstanceId WorkflowInstanceId { get; }

    TaskTemplateId TaskTemplateId { get; }

    TaskInstanceId TaskInstanceId { get; }

    TaskInstanceId? SpawnedByTaskInstanceId { get; }

    TaskSpecification TaskSpecification { get; }

    IReadOnlyDictionary<TaskInstanceId, TaskStatus> DependencyStatuses { get; }

    IReadOnlyDictionary<TaskInstanceId, ExecutionOutput?> DependencyOutputs { get; }
}