namespace OrganizeMedia.Tests;

using OrganizeMedia.Framework;

internal static class WorkflowTestSupport
{
    internal static Workflow FromAdjacencyArray(
        string workflowIdString,
        int[][] adjacencyArray,
        int? maxConcurrency = null,
        string taskType = "StaticTask")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowIdString);
        ArgumentNullException.ThrowIfNull(adjacencyArray);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskType);

        WorkflowId workflowId = new WorkflowId(workflowIdString);
        List<TaskSpecification> taskSpecifications = new List<TaskSpecification>();
        List<TaskDependencySpecification> dependencySpecifications = new List<TaskDependencySpecification>();

        for (int i = 0; i < adjacencyArray.Length; i++)
        {
            TaskId taskId = new TaskId("T" + i.ToString());
            taskSpecifications.Add(
                new TaskSpecification(
                    TaskId: taskId,
                    TaskType: new TaskType(taskType),
                    InputType: new InputType("application/json"),
                    InputJson: $"{{\"taskIndex\":{i}}}"));
        }

        for (int i = 0; i < adjacencyArray.Length; i++)
        {
            int[] adjacentTaskIndices = adjacencyArray[i] ?? Array.Empty<int>();

            for (int j = 0; j < adjacentTaskIndices.Length; j++)
            {
                int adjacentIndex = adjacentTaskIndices[j];
                dependencySpecifications.Add(
                    new TaskDependencySpecification(
                        PrerequisiteTaskId: new TaskId("T" + i.ToString()),
                        DependentTaskId: new TaskId("T" + adjacentIndex.ToString())));
            }
        }

        WorkflowSpecification specification = new WorkflowSpecification(
            WorkflowId: workflowId,
            Tasks: taskSpecifications,
            Dependencies: dependencySpecifications,
            MaxConcurrency: maxConcurrency);

        return Workflow.FromSpecification(specification);
    }

    internal static Workflow CreateWorkflow(
        string workflowId,
        IReadOnlyCollection<string> taskIds,
        IReadOnlyCollection<TaskDependencySpecification> dependencies)
    {
        WorkflowSpecification specification = new WorkflowSpecification(
            WorkflowId: new WorkflowId(workflowId),
            Tasks: taskIds
                .Select(taskId => new TaskSpecification(new TaskId(taskId), new TaskType(taskId)))
                .ToArray(),
            Dependencies: dependencies.ToArray());

        return Workflow.FromSpecification(specification);
    }
}

internal sealed class RecordingFakeTaskExecutor : ITaskExecutor
{
    private readonly Dictionary<string, Queue<TaskExecutionResult>> _resultsByTaskId;

    public RecordingFakeTaskExecutor(Dictionary<string, IReadOnlyList<TaskExecutionResult>> resultsByTaskId)
    {
        _resultsByTaskId = resultsByTaskId.ToDictionary(
            pair => pair.Key,
            pair => new Queue<TaskExecutionResult>(pair.Value));
    }

    public List<string> ExecutedTaskIds { get; } = new List<string>();

    public TaskExecutionResult Execute(IExecutionContext executionContext)
    {
        string taskId = executionContext.TaskId.Value;
        ExecutedTaskIds.Add(taskId);

        if (!_resultsByTaskId.TryGetValue(taskId, out Queue<TaskExecutionResult>? results) || results.Count == 0)
        {
            throw new InvalidOperationException($"No configured execution result for task {taskId}.");
        }

        return results.Dequeue();
    }
}

internal sealed class DynamicSpawnAndJoinFakeTaskExecutor : ITaskExecutor
{
    private static readonly TaskSpecification[] SpawnedProcessTasks =
    [
        CreateProcessTask("B-1", "a.mp4"),
        CreateProcessTask("B-2", "b.mp4"),
        CreateProcessTask("B-3", "c.mp4")
    ];

    public List<string> ExecutedTaskIds { get; } = new List<string>();

    public Dictionary<string, TaskId?> SpawnedByTaskIds { get; } = new Dictionary<string, TaskId?>();

    public List<string> AggregatorDependencyTaskIds { get; } = new List<string>();

    public List<string> AggregatorDependencyOutputValues { get; } = new List<string>();

    public TaskExecutionResult Execute(IExecutionContext executionContext)
    {
        string taskId = executionContext.TaskId.Value;
        ExecutedTaskIds.Add(taskId);

        return taskId switch
        {
            "A" => TaskExecutionResult.Succeeded(
                output: new TextExecutionOutput("Discovered 3 mp4 files"),
                spawnedTasks: SpawnedProcessTasks,
                addedDependencies: CreateJoinDependencies(SpawnedProcessTasks, new TaskId("C"))),
            "B-1" => RecordSpawnedTaskAndReturnResult(executionContext, "a.mp4"),
            "B-2" => RecordSpawnedTaskAndReturnResult(executionContext, "b.mp4"),
            "B-3" => RecordSpawnedTaskAndReturnResult(executionContext, "c.mp4"),
            "C" => RecordAggregatorInputsAndReturnResult(executionContext),
            _ => throw new InvalidOperationException($"Unexpected task {taskId}.")
        };
    }

    private TaskExecutionResult RecordSpawnedTaskAndReturnResult(IExecutionContext executionContext, string fileName)
    {
        SpawnedByTaskIds[executionContext.TaskId.Value] = executionContext.TaskSpecification.SpawnedByTaskId;
        return TaskExecutionResult.Succeeded(new TextExecutionOutput($"processed:{fileName}"));
    }

    private TaskExecutionResult RecordAggregatorInputsAndReturnResult(IExecutionContext executionContext)
    {
        foreach (TaskInstanceId dependencyTaskId in executionContext.DependencyStatuses.Keys.OrderBy(taskId => taskId.ToString()))
        {
            AggregatorDependencyTaskIds.Add(dependencyTaskId.ToString());
        }

        foreach (ExecutionOutput? output in executionContext.DependencyOutputs
            .OrderBy(pair => pair.Key.ToString())
            .Select(pair => pair.Value))
        {
            Assert.NotNull(output);
            AggregatorDependencyOutputValues.Add(((TextExecutionOutput)output).Value);
        }

        return TaskExecutionResult.Succeeded(new TextExecutionOutput("aggregated"));
    }

    private static TaskSpecification CreateProcessTask(string taskId, string fileName)
    {
        return new TaskSpecification(
            TaskId: new TaskId(taskId),
            TaskType: new TaskType("ProcessMp4"),
            InputType: new InputType("application/json"),
            InputJson: $"{{ \"file\": \"{fileName}\" }}");
    }

    private static TaskDependencySpecification[] CreateJoinDependencies(
        IReadOnlyCollection<TaskSpecification> spawnedTasks,
        TaskId joinTaskId)
    {
        return spawnedTasks
            .Select(taskSpecification => new TaskDependencySpecification(taskSpecification.TaskId, joinTaskId))
            .ToArray();
    }
}