namespace OrganizeMedia.Tests;

using OrganizeMedia.Framework;

public class WorkflowValidationTests
{
    [Fact]
    public void TaskExecutionResult_SucceededWithoutMutations_UsesNoneMutationPayload()
    {
        TaskExecutionResult result = TaskExecutionResult.Succeeded();

        Assert.Same(TaskGraphChanges.None, result.GraphChanges);
        Assert.Empty(result.GraphChanges.SpawnedTasks);
        Assert.Empty(result.GraphChanges.AddedDependencies);
    }

    [Fact]
    public void TaskExecutionResult_SucceededWithEmptyMutationCollections_UsesNoneMutationPayload()
    {
        TaskExecutionResult result = TaskExecutionResult.Succeeded(
            spawnedTasks: Array.Empty<TaskSpecification>(),
            addedDependencies: Array.Empty<TaskDependencySpecification>());

        Assert.Same(TaskGraphChanges.None, result.GraphChanges);
        Assert.Empty(result.GraphChanges.SpawnedTasks);
        Assert.Empty(result.GraphChanges.AddedDependencies);
    }

    [Fact]
    public void TaskExecutionResult_Canceled_RejectsNonTerminalRecoverability()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => TaskExecutionResult.Canceled(recoverability: ExecutionRecoverability.AwaitingOutcome));

        Assert.Contains("terminal recoverability", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TaskExecutionResult_Failed_RejectsNonTerminalRecoverability()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => TaskExecutionResult.Failed(
                ExecutionFailureKind.Transient,
                recoverability: ExecutionRecoverability.NoRecoveryNeeded));

        Assert.Contains("terminal recoverability", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TaskExecutionResult_Canceled_UsesNoneMutationPayload()
    {
        TaskExecutionResult result = TaskExecutionResult.Canceled();

        Assert.Same(TaskGraphChanges.None, result.GraphChanges);
        Assert.Empty(result.GraphChanges.SpawnedTasks);
        Assert.Empty(result.GraphChanges.AddedDependencies);
    }

    [Fact]
    public void TaskExecutionResult_Failed_UsesNoneMutationPayload()
    {
        TaskExecutionResult result = TaskExecutionResult.Failed(ExecutionFailureKind.Transient);

        Assert.Same(TaskGraphChanges.None, result.GraphChanges);
        Assert.Empty(result.GraphChanges.SpawnedTasks);
        Assert.Empty(result.GraphChanges.AddedDependencies);
    }

    [Fact]
    public void FromAdjacencyArray_RejectsDuplicateDependencies()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowTestSupport.FromAdjacencyArray("DuplicateDependency", [[1, 1], []]));

        Assert.Contains("duplicate dependency", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromAdjacencyArray_RejectsCycles()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => WorkflowTestSupport.FromAdjacencyArray("Cycle", [[1], [0]]));

        Assert.Contains("cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TaskSpecification_SingletonCardinality_DefaultsToOneInitialInstance()
    {
        TaskSpecification specification = new TaskSpecification(
            TaskTemplateId: new TaskTemplateId("A"),
            TaskType: new TaskType("SingletonTask"));

        Assert.Equal(TaskCardinality.Singleton, specification.Cardinality);
        Assert.Equal(1, specification.InitialInstanceCount);
    }

    [Fact]
    public void TaskSpecification_ZeroToManyCardinality_DefaultsToZeroInitialInstances()
    {
        TaskSpecification specification = new TaskSpecification(
            TaskTemplateId: new TaskTemplateId("B"),
            TaskType: new TaskType("DynamicTask"),
            Cardinality: TaskCardinality.ZeroToMany);

        Assert.Equal(TaskCardinality.ZeroToMany, specification.Cardinality);
        Assert.Equal(0, specification.InitialInstanceCount);
    }

    [Fact]
    public void Workflow_FromSpecification_DoesNotCreateInitialInstancesForZeroToManyTemplates()
    {
        Workflow workflow = Workflow.FromSpecification(
            new WorkflowSpecification(
                WorkflowTemplateId: new WorkflowTemplateId("CardinalityValidation"),
                Tasks:
                [
                    new TaskSpecification(
                        TaskTemplateId: new TaskTemplateId("A"),
                        TaskType: new TaskType("DiscoverFiles")),
                    new TaskSpecification(
                        TaskTemplateId: new TaskTemplateId("B"),
                        TaskType: new TaskType("ExtractFileMetadata"),
                        Cardinality: TaskCardinality.ZeroToMany)
                ],
                Dependencies:
                [
                    new TaskDependencySpecification(new TaskTemplateId("A"), new TaskTemplateId("B"))
                ]));

        Assert.Single(workflow.Status.TaskStatuses.Values, status => status.TaskTemplateId == new TaskTemplateId("A"));
        Assert.DoesNotContain(workflow.Status.TaskStatuses.Values, status => status.TaskTemplateId == new TaskTemplateId("B"));
    }

    [Fact]
    public void PublicMembers_ExposeTaskTemplateIdSemantics()
    {
        WorkflowTemplateId workflowTemplateId = new WorkflowTemplateId("W0");
        WorkflowInstanceId workflowInstanceId = new WorkflowInstanceId(workflowTemplateId, 1);

        TaskSpecification specification = new TaskSpecification(
            TaskTemplateId: new TaskTemplateId("ExtractFileMetadata"),
            TaskType: new TaskType("ExtractFileMetadata"),
            Cardinality: TaskCardinality.ZeroToMany);

        TaskStatus status = new TaskStatus(
            WorkflowTemplateId: workflowTemplateId,
            WorkflowInstanceId: workflowInstanceId,
            TaskTemplateId: specification.TaskTemplateId,
            TaskInstanceId: new TaskInstanceId(specification.TaskTemplateId, 1));

        TaskTransitionEvent transitionEvent = new TaskTransitionEvent(
            WorkflowTemplateId: workflowTemplateId,
            WorkflowInstanceId: workflowInstanceId,
            TaskTemplateId: specification.TaskTemplateId,
            TaskInstanceId: new TaskInstanceId(specification.TaskTemplateId, 1),
            PreviousStatus: status,
            CurrentStatus: status,
            Timestamp: DateTime.UtcNow);

        TaskAddedEvent addedEvent = new TaskAddedEvent(
            WorkflowTemplateId: workflowTemplateId,
            WorkflowInstanceId: workflowInstanceId,
            TaskTemplateId: specification.TaskTemplateId,
            TaskInstanceId: new TaskInstanceId(specification.TaskTemplateId, 1),
            Timestamp: DateTime.UtcNow);

        TaskDependencySpecification dependency = new TaskDependencySpecification(
            PrerequisiteTaskTemplateId: new TaskTemplateId("DiscoverFiles"),
            DependentTaskTemplateId: new TaskTemplateId("ExtractFileMetadata"));

        Assert.Equal(new TaskTemplateId("ExtractFileMetadata"), specification.TaskTemplateId);
        Assert.Equal(specification.TaskTemplateId, status.TaskTemplateId);
        Assert.Equal(specification.TaskTemplateId, transitionEvent.TaskTemplateId);
        Assert.Equal(specification.TaskTemplateId, addedEvent.TaskTemplateId);
        Assert.Equal(new TaskTemplateId("DiscoverFiles"), dependency.PrerequisiteTaskTemplateId);
        Assert.Equal(new TaskTemplateId("ExtractFileMetadata"), dependency.DependentTaskTemplateId);
    }
}