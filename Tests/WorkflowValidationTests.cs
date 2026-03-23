namespace OrganizeMedia.Tests;

using OrganizeMedia.Framework;

public class WorkflowValidationTests
{
    [Fact]
    public void TaskExecutionResult_SucceededWithoutMutations_UsesNoneMutationPayload()
    {
        TaskExecutionResult result = TaskExecutionResult.Succeeded();

        Assert.Same(TaskRuntimeMutations.None, result.RuntimeMutations);
        Assert.Empty(result.RuntimeMutations.SpawnedTasks);
        Assert.Empty(result.RuntimeMutations.AddedDependencies);
    }

    [Fact]
    public void TaskExecutionResult_SucceededWithEmptyMutationCollections_UsesNoneMutationPayload()
    {
        TaskExecutionResult result = TaskExecutionResult.Succeeded(
            spawnedTasks: Array.Empty<TaskSpecification>(),
            addedDependencies: Array.Empty<TaskDependencySpecification>());

        Assert.Same(TaskRuntimeMutations.None, result.RuntimeMutations);
        Assert.Empty(result.RuntimeMutations.SpawnedTasks);
        Assert.Empty(result.RuntimeMutations.AddedDependencies);
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

        Assert.Same(TaskRuntimeMutations.None, result.RuntimeMutations);
        Assert.Empty(result.RuntimeMutations.SpawnedTasks);
        Assert.Empty(result.RuntimeMutations.AddedDependencies);
    }

    [Fact]
    public void TaskExecutionResult_Failed_UsesNoneMutationPayload()
    {
        TaskExecutionResult result = TaskExecutionResult.Failed(ExecutionFailureKind.Transient);

        Assert.Same(TaskRuntimeMutations.None, result.RuntimeMutations);
        Assert.Empty(result.RuntimeMutations.SpawnedTasks);
        Assert.Empty(result.RuntimeMutations.AddedDependencies);
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