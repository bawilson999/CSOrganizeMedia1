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
}