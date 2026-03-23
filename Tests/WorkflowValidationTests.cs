namespace OrganizeMedia.Tests;

using OrganizeMedia.Framework;

public class WorkflowValidationTests
{
    [Fact]
    public void TaskExecutionResult_SucceededWithoutMutations_UsesNoneMutationPayload()
    {
        TaskExecutionResult result = TaskExecutionResult.Succeeded();

        Assert.Same(WorkflowGraphChanges.None, result.GraphChanges);
        Assert.Empty(result.GraphChanges.SpawnedTasks);
        Assert.Empty(result.GraphChanges.AddedDependencies);
    }

    [Fact]
    public void TaskExecutionResult_SucceededWithEmptyMutationCollections_UsesNoneMutationPayload()
    {
        TaskExecutionResult result = TaskExecutionResult.Succeeded(
            spawnedTasks: Array.Empty<TaskSpecification>(),
            addedDependencies: Array.Empty<TaskDependencySpecification>());

        Assert.Same(WorkflowGraphChanges.None, result.GraphChanges);
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

        Assert.Same(WorkflowGraphChanges.None, result.GraphChanges);
        Assert.Empty(result.GraphChanges.SpawnedTasks);
        Assert.Empty(result.GraphChanges.AddedDependencies);
    }

    [Fact]
    public void TaskExecutionResult_Failed_UsesNoneMutationPayload()
    {
        TaskExecutionResult result = TaskExecutionResult.Failed(ExecutionFailureKind.Transient);

        Assert.Same(WorkflowGraphChanges.None, result.GraphChanges);
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
            TaskSpecificationId: new TaskSpecificationId("A"),
            TaskType: new TaskType("SingletonTask"));

        Assert.Equal(TaskCardinality.Singleton, specification.Cardinality);
        Assert.Equal(1, specification.InitialInstanceCount);
    }

    [Fact]
    public void TaskSpecification_ZeroToManyCardinality_DefaultsToZeroInitialInstances()
    {
        TaskSpecification specification = new TaskSpecification(
            TaskSpecificationId: new TaskSpecificationId("B"),
            TaskType: new TaskType("DynamicTask"),
            Cardinality: TaskCardinality.ZeroToMany);

        Assert.Equal(TaskCardinality.ZeroToMany, specification.Cardinality);
        Assert.Equal(0, specification.InitialInstanceCount);
    }

    [Fact]
    public void Workflow_FromSpecification_DoesNotCreateInitialInstancesForZeroToManySpecifications()
    {
        Workflow workflow = Workflow.FromSpecification(
            new WorkflowSpecification(
                WorkflowSpecificationId: new WorkflowSpecificationId("CardinalityValidation"),
                Tasks:
                [
                    new TaskSpecification(
                        TaskSpecificationId: new TaskSpecificationId("A"),
                        TaskType: new TaskType("DiscoverFiles")),
                    new TaskSpecification(
                        TaskSpecificationId: new TaskSpecificationId("B"),
                        TaskType: new TaskType("ExtractFileMetadata"),
                        Cardinality: TaskCardinality.ZeroToMany)
                ],
                Dependencies:
                [
                    new TaskDependencySpecification(new TaskSpecificationId("A"), new TaskSpecificationId("B"))
                ]));

        Assert.Single(workflow.Status.TaskStatuses.Values, status => status.TaskSpecificationId == new TaskSpecificationId("A"));
        Assert.DoesNotContain(workflow.Status.TaskStatuses.Values, status => status.TaskSpecificationId == new TaskSpecificationId("B"));
    }

    [Fact]
    public void WorkflowSpecification_DefensivelyCopiesInputCollections()
    {
        List<TaskSpecification> tasks =
        [
            new TaskSpecification(
                TaskSpecificationId: new TaskSpecificationId("A"),
                TaskType: new TaskType("DiscoverFiles"))
        ];

        List<TaskDependencySpecification> dependencies =
        [
            new TaskDependencySpecification(
                PrerequisiteTaskSpecificationId: new TaskSpecificationId("A"),
                DependentTaskSpecificationId: new TaskSpecificationId("B"))
        ];

        WorkflowSpecification specification = new WorkflowSpecification(
            WorkflowSpecificationId: new WorkflowSpecificationId("W0"),
            Tasks: tasks,
            Dependencies: dependencies);

        tasks.Add(new TaskSpecification(
            TaskSpecificationId: new TaskSpecificationId("C"),
            TaskType: new TaskType("GenerateReport")));

        dependencies.Clear();

        Assert.Single(specification.Tasks);
        Assert.Single(specification.Dependencies);
    }

    [Fact]
    public void PublicMembers_ExposeTaskSpecificationIdSemantics()
    {
        WorkflowSpecificationId workflowSpecificationId = new WorkflowSpecificationId("W0");
        WorkflowInstanceId workflowInstanceId = new WorkflowInstanceId(workflowSpecificationId, 1);

        TaskSpecification specification = new TaskSpecification(
            TaskSpecificationId: new TaskSpecificationId("ExtractFileMetadata"),
            TaskType: new TaskType("ExtractFileMetadata"),
            Cardinality: TaskCardinality.ZeroToMany);

        TaskStatus status = new TaskStatus(
            WorkflowSpecificationId: workflowSpecificationId,
            WorkflowInstanceId: workflowInstanceId,
            TaskSpecificationId: specification.TaskSpecificationId,
            TaskInstanceId: new TaskInstanceId(specification.TaskSpecificationId, 1));

        TaskTransitionEvent transitionEvent = new TaskTransitionEvent(
            WorkflowSpecificationId: workflowSpecificationId,
            WorkflowInstanceId: workflowInstanceId,
            TaskSpecificationId: specification.TaskSpecificationId,
            TaskInstanceId: new TaskInstanceId(specification.TaskSpecificationId, 1),
            PreviousStatus: status,
            CurrentStatus: status,
            Timestamp: DateTime.UtcNow);

        TaskAddedEvent addedEvent = new TaskAddedEvent(
            WorkflowSpecificationId: workflowSpecificationId,
            WorkflowInstanceId: workflowInstanceId,
            TaskSpecificationId: specification.TaskSpecificationId,
            TaskInstanceId: new TaskInstanceId(specification.TaskSpecificationId, 1),
            Timestamp: DateTime.UtcNow);

        TaskDependencySpecification dependency = new TaskDependencySpecification(
            PrerequisiteTaskSpecificationId: new TaskSpecificationId("DiscoverFiles"),
            DependentTaskSpecificationId: new TaskSpecificationId("ExtractFileMetadata"));

        Assert.Equal(new TaskSpecificationId("ExtractFileMetadata"), specification.TaskSpecificationId);
        Assert.Equal(specification.TaskSpecificationId, status.TaskSpecificationId);
        Assert.Equal(specification.TaskSpecificationId, transitionEvent.TaskSpecificationId);
        Assert.Equal(specification.TaskSpecificationId, addedEvent.TaskSpecificationId);
        Assert.Equal(new TaskSpecificationId("DiscoverFiles"), dependency.PrerequisiteTaskSpecificationId);
        Assert.Equal(new TaskSpecificationId("ExtractFileMetadata"), dependency.DependentTaskSpecificationId);
    }
}