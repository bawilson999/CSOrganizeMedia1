namespace OrganizeMedia.Tests;

using OrganizeMedia.Framework;

public class WorkflowExamplesTests
{
    [Fact]
    public void RunToCompletion_ProgramExampleWorkflow_CompletesInDependencySafeOrder()
    {
        Workflow workflow = WorkflowTestSupport.FromAdjacencyArray("W0", [[1], [], [1], [2], [2]]);

        RecordingFakeTaskExecutor taskExecutor = new RecordingFakeTaskExecutor(
            new Dictionary<string, IReadOnlyList<TaskExecutionResult>>
            {
                ["T0"] = [TaskExecutionResult.Succeeded(new TextExecutionOutput("T0 complete"))],
                ["T1"] = [TaskExecutionResult.Succeeded(new TextExecutionOutput("T1 complete"))],
                ["T2"] = [TaskExecutionResult.Succeeded(new TextExecutionOutput("T2 complete"))],
                ["T3"] = [TaskExecutionResult.Succeeded(new TextExecutionOutput("T3 complete"))],
                ["T4"] = [TaskExecutionResult.Succeeded(new TextExecutionOutput("T4 complete"))]
            });

        workflow.RunToCompletion(taskExecutor);

        Assert.Equal(ExecutionOutcome.Succeeded, workflow.Status.ExecutionOutcome);
        Assert.Equal(["T0", "T3", "T4", "T2", "T1"], taskExecutor.ExecutedTaskIds);
        Assert.All(
            workflow.Status.TaskStatuses.Values,
            taskStatus => Assert.Equal(ExecutionOutcome.Succeeded, taskStatus.ExecutionOutcome));
    }

    [Fact]
    public void RunToCompletion_DynamicSpawnAndJoin_ExecutesJoinAfterSpawnedTasks()
    {
        WorkflowSpecification specification = new WorkflowSpecification(
            WorkflowTemplateId: new WorkflowTemplateId("Mp4Workflow"),
            Tasks:
            [
                new TaskSpecification(
                    TaskTemplateId: new TaskTemplateId("A"),
                    TaskType: new TaskType("ScanMp4Directory"),
                    InputType: new InputType("application/json"),
                    InputJson: "{ \"path\": \"c:/media\" }"),
                new TaskSpecification(
                    TaskTemplateId: new TaskTemplateId("C"),
                    TaskType: new TaskType("AggregateMp4Results"))
            ],
            Dependencies: Array.Empty<TaskDependencySpecification>(),
            MaxConcurrency: 4);

        Workflow workflow = Workflow.FromSpecification(specification);
        DynamicSpawnAndJoinFakeTaskExecutor taskExecutor = new DynamicSpawnAndJoinFakeTaskExecutor();

        workflow.RunToCompletion(taskExecutor);

        Assert.Equal(ExecutionOutcome.Succeeded, workflow.Status.ExecutionOutcome);
        Assert.Equal(["A", "B-1", "B-2", "B-3", "C"], taskExecutor.ExecutedTaskIds);
        Assert.Equal(new TaskInstanceId(new TaskTemplateId("A"), 1), taskExecutor.SpawnedByTaskInstanceIds["B-1"]);
        Assert.Equal(new TaskInstanceId(new TaskTemplateId("A"), 1), taskExecutor.SpawnedByTaskInstanceIds["B-2"]);
        Assert.Equal(new TaskInstanceId(new TaskTemplateId("A"), 1), taskExecutor.SpawnedByTaskInstanceIds["B-3"]);
        Assert.Equal(["B-1/1", "B-2/1", "B-3/1"], taskExecutor.AggregatorDependencyTaskIds);
        Assert.Equal(["processed:a.mp4", "processed:b.mp4", "processed:c.mp4"], taskExecutor.AggregatorDependencyOutputValues);
        Assert.Equal(5, workflow.Status.TaskStatuses.Count);
        Assert.Equal(
            ExecutionOutcome.Succeeded,
            workflow.Status.TaskStatuses[new TaskInstanceId(new TaskTemplateId("C"), 1)].ExecutionOutcome);
    }

    [Fact]
    public void RunToCompletion_SpawnedTasksWithSameTemplateId_GetDistinctInstanceIds()
    {
        WorkflowSpecification specification = new WorkflowSpecification(
            WorkflowTemplateId: new WorkflowTemplateId("TemplateInstanceWorkflow"),
            Tasks:
            [
                new TaskSpecification(
                    TaskTemplateId: new TaskTemplateId("A"),
                    TaskType: new TaskType("DiscoverFiles"))
            ],
            Dependencies: Array.Empty<TaskDependencySpecification>());

        Workflow workflow = Workflow.FromSpecification(specification);
        RepeatedTemplateSpawnFakeTaskExecutor taskExecutor = new RepeatedTemplateSpawnFakeTaskExecutor();

        workflow.RunToCompletion(taskExecutor);

        Assert.Equal(ExecutionOutcome.Succeeded, workflow.Status.ExecutionOutcome);
        Assert.Equal(["A", "B", "B"], taskExecutor.ExecutedTaskIds);

        TaskStatus[] repeatedTaskStatuses = workflow.Status.TaskStatuses.Values
            .Where(status => status.TaskTemplateId == new TaskTemplateId("B"))
            .OrderBy(status => status.TaskInstanceId.InstanceNumber)
            .ToArray();

        Assert.Equal(2, repeatedTaskStatuses.Length);
        Assert.Equal(
            [1, 2],
            repeatedTaskStatuses.Select(status => status.TaskInstanceId.InstanceNumber).ToArray());
        Assert.Equal(
            ["{ \"file\": \"a.mp4\" }", "{ \"file\": \"b.mp4\" }"],
            repeatedTaskStatuses.Select(status => ((TextExecutionOutput)status.Output!).Value).OrderBy(value => value).ToArray());
    }

    private sealed class RepeatedTemplateSpawnFakeTaskExecutor : ITaskExecutor
    {
        public List<string> ExecutedTaskIds { get; } = new List<string>();

        public TaskExecutionResult Execute(IExecutionContext executionContext)
        {
            ExecutedTaskIds.Add(executionContext.TaskTemplateId.Value);

            return executionContext.TaskTemplateId.Value switch
            {
                "A" => TaskExecutionResult.Succeeded(
                    spawnedTasks:
                    [
                        new TaskSpecification(
                            TaskTemplateId: new TaskTemplateId("B"),
                            TaskType: new TaskType("ExtractFileMetadata"),
                            InputType: new InputType("application/json"),
                            InputJson: "{ \"file\": \"a.mp4\" }"),
                        new TaskSpecification(
                            TaskTemplateId: new TaskTemplateId("B"),
                            TaskType: new TaskType("ExtractFileMetadata"),
                            InputType: new InputType("application/json"),
                            InputJson: "{ \"file\": \"b.mp4\" }")
                    ]),
                "B" => TaskExecutionResult.Succeeded(
                    new TextExecutionOutput(executionContext.TaskSpecification.InputJson!)),
                _ => throw new InvalidOperationException($"Unexpected task {executionContext.TaskTemplateId}.")
            };
        }
    }

    [Fact]
    public void RunToCompletion_MetadataReportTemplates_CreateRuntimeInstanceGraph()
    {
        WorkflowSpecification specification = new WorkflowSpecification(
            WorkflowTemplateId: new WorkflowTemplateId("MetadataReport"),
            Tasks:
            [
                new TaskSpecification(
                    TaskTemplateId: new TaskTemplateId("DiscoverFiles"),
                    TaskType: new TaskType("DiscoverFiles"),
                    InputType: new InputType("application/json"),
                    InputJson: "{ \"root\": \"Z:/Remotefiles\", \"pattern\": \"*.mp4\", \"searchOption\": \"Recursive\" }"),
                new TaskSpecification(
                    TaskTemplateId: new TaskTemplateId("ExtractFileMetadata"),
                    TaskType: new TaskType("ExtractFileMetadata"),
                    Cardinality: TaskCardinality.ZeroToMany),
                new TaskSpecification(
                    TaskTemplateId: new TaskTemplateId("SaveFileMetadata"),
                    TaskType: new TaskType("SaveFileMetadata"),
                    Cardinality: TaskCardinality.ZeroToMany),
                new TaskSpecification(
                    TaskTemplateId: new TaskTemplateId("GenerateMetadataReport"),
                    TaskType: new TaskType("GenerateMetadataReport"))
            ],
            Dependencies:
            [
                new TaskDependencySpecification(new TaskTemplateId("DiscoverFiles"), new TaskTemplateId("ExtractFileMetadata")),
                new TaskDependencySpecification(new TaskTemplateId("ExtractFileMetadata"), new TaskTemplateId("SaveFileMetadata")),
                new TaskDependencySpecification(new TaskTemplateId("ExtractFileMetadata"), new TaskTemplateId("GenerateMetadataReport"))
            ],
            MaxConcurrency: 6);

        Workflow workflow = Workflow.FromSpecification(specification);
        MetadataReportFakeTaskExecutor taskExecutor = new MetadataReportFakeTaskExecutor();

        workflow.RunToCompletion(taskExecutor);

        Assert.Equal(ExecutionOutcome.Succeeded, workflow.Status.ExecutionOutcome);
        Assert.Single(workflow.Status.TaskStatuses.Values, status => status.TaskTemplateId == new TaskTemplateId("DiscoverFiles"));
        Assert.Equal(4, workflow.Status.TaskStatuses.Values.Count(status => status.TaskTemplateId == new TaskTemplateId("ExtractFileMetadata")));
        Assert.Equal(4, workflow.Status.TaskStatuses.Values.Count(status => status.TaskTemplateId == new TaskTemplateId("SaveFileMetadata")));
        Assert.Single(workflow.Status.TaskStatuses.Values, status => status.TaskTemplateId == new TaskTemplateId("GenerateMetadataReport"));

        TaskStatus reportStatus = workflow.Status.TaskStatuses[new TaskInstanceId(new TaskTemplateId("GenerateMetadataReport"), 1)];
        string reportJson = Assert.IsType<TextExecutionOutput>(reportStatus.Output).Value;
        Assert.Contains("\"FileCount\":4", reportJson, StringComparison.Ordinal);
        Assert.Contains("\"TotalFileSize\":3160887", reportJson, StringComparison.Ordinal);
        Assert.Contains("\"TotalFrameCount\":3923", reportJson, StringComparison.Ordinal);
        Assert.Contains("Z:/Remotefiles/File4.mp4", reportJson, StringComparison.Ordinal);
        Assert.Equal(4, taskExecutor.ReportDependencyInstanceIds.Count);
        Assert.All(taskExecutor.ReportDependencyInstanceIds, id => Assert.StartsWith("ExtractFileMetadata/", id, StringComparison.Ordinal));
        Assert.Equal(4, taskExecutor.SavedMetadataPaths.Count);
    }

    private sealed class MetadataReportFakeTaskExecutor : ITaskExecutor
    {
        private static readonly (string FilePath, int FileSize, int FrameCount)[] MetadataRows =
        [
            ("Z:/Remotefiles/File1.mp4", 1234567, 1567),
            ("Z:/Remotefiles/File2.mp4", 1432111, 1789),
            ("Z:/Remotefiles/File3.mp4", 234321, 567),
            ("Z:/Remotefiles/File4.mp4", 259888, 0)
        ];

        public List<string> ReportDependencyInstanceIds { get; } = new List<string>();

        public List<string> SavedMetadataPaths { get; } = new List<string>();

        public TaskExecutionResult Execute(IExecutionContext executionContext)
        {
            return executionContext.TaskTemplateId.Value switch
            {
                "DiscoverFiles" => DiscoverFiles(executionContext),
                "ExtractFileMetadata" => ExtractFileMetadata(executionContext),
                "SaveFileMetadata" => SaveFileMetadata(executionContext),
                "GenerateMetadataReport" => GenerateMetadataReport(executionContext),
                _ => throw new InvalidOperationException($"Unexpected task {executionContext.TaskInstanceId}.")
            };
        }

        private TaskExecutionResult DiscoverFiles(IExecutionContext executionContext)
        {
            TaskTemplateSpawn[] extractSpawns = MetadataRows
                .Select((row, index) => new TaskTemplateSpawn(
                    SpawnKey: $"extract-{index + 1}",
                    TaskTemplateId: new TaskTemplateId("ExtractFileMetadata"),
                    InputType: new InputType("application/json"),
                    InputJson: $"{{ \"filePath\": \"{row.FilePath}\" }}"))
                .ToArray();

            TaskInstanceDependency[] instanceDependencies = extractSpawns
                .Select(spawn => new[]
                {
                    new TaskInstanceDependency(
                        Prerequisite: TaskNodeReference.TaskInstance(executionContext.TaskInstanceId),
                        Dependent: TaskNodeReference.SpawnedTask(spawn.SpawnKey)),
                    new TaskInstanceDependency(
                        Prerequisite: TaskNodeReference.SpawnedTask(spawn.SpawnKey),
                        Dependent: TaskNodeReference.TemplateTask(new TaskTemplateId("GenerateMetadataReport")))
                })
                .SelectMany(pair => pair)
                .ToArray();

            string discoveredFilesJson = "[" + string.Join(",", MetadataRows.Select(row => $"\"{row.FilePath}\"")) + "]";

            return TaskExecutionResult.Succeeded(
                output: new TextExecutionOutput(discoveredFilesJson),
                spawnedTaskTemplates: extractSpawns,
                addedInstanceDependencies: instanceDependencies);
        }

        private TaskExecutionResult ExtractFileMetadata(IExecutionContext executionContext)
        {
            string inputJson = executionContext.TaskSpecification.InputJson!;
            string filePath = ExtractJsonValue(inputJson, "filePath");
            (string FilePath, int FileSize, int FrameCount) metadata = MetadataRows.Single(row => row.FilePath == filePath);

            string metadataJson =
                $"{{ \"filePath\": \"{metadata.FilePath}\", \"fileSize\": {metadata.FileSize}, \"frameCount\": {metadata.FrameCount} }}";

            TaskTemplateSpawn saveSpawn = new TaskTemplateSpawn(
                SpawnKey: $"save-{executionContext.TaskInstanceId.InstanceNumber}",
                TaskTemplateId: new TaskTemplateId("SaveFileMetadata"),
                InputType: new InputType("application/json"),
                InputJson: metadataJson);

            TaskInstanceDependency saveDependency = new TaskInstanceDependency(
                Prerequisite: TaskNodeReference.TaskInstance(executionContext.TaskInstanceId),
                Dependent: TaskNodeReference.SpawnedTask(saveSpawn.SpawnKey));

            return TaskExecutionResult.Succeeded(
                output: new TextExecutionOutput(metadataJson),
                spawnedTaskTemplates: [saveSpawn],
                addedInstanceDependencies: [saveDependency]);
        }

        private TaskExecutionResult SaveFileMetadata(IExecutionContext executionContext)
        {
            string metadataJson = executionContext.TaskSpecification.InputJson!;
            string filePath = ExtractJsonValue(metadataJson, "filePath");
            string savePath = filePath.Replace(".mp4", ".metadata", StringComparison.Ordinal);
            SavedMetadataPaths.Add(savePath);
            return TaskExecutionResult.Succeeded(new TextExecutionOutput(savePath));
        }

        private TaskExecutionResult GenerateMetadataReport(IExecutionContext executionContext)
        {
            foreach (TaskInstanceId dependencyInstanceId in executionContext.DependencyOutputs.Keys.OrderBy(id => id.InstanceNumber))
            {
                ReportDependencyInstanceIds.Add(dependencyInstanceId.ToString());
            }

            (int FileCount, int TotalFileSize, int TotalFrameCount, List<string> CorruptFiles) aggregate = executionContext.DependencyOutputs
                .OrderBy(pair => pair.Key.InstanceNumber)
                .Select(pair => Assert.IsType<TextExecutionOutput>(pair.Value))
                .Select(output => ParseMetadata(output.Value))
                .Aggregate(
                    (FileCount: 0, TotalFileSize: 0, TotalFrameCount: 0, CorruptFiles: new List<string>()),
                    (state, row) =>
                    {
                        state.FileCount += 1;
                        state.TotalFileSize += row.FileSize;
                        state.TotalFrameCount += row.FrameCount;
                        if (row.FrameCount == 0)
                        {
                            state.CorruptFiles.Add(row.FilePath);
                        }

                        return state;
                    });

            string reportJson =
                $"{{ \"FileCount\":{aggregate.FileCount}, \"TotalFileSize\":{aggregate.TotalFileSize}, \"TotalFrameCount\":{aggregate.TotalFrameCount}, \"CorruptFiles\":[{string.Join(",", aggregate.CorruptFiles.Select(path => $"\"{path}\""))}] }}";

            return TaskExecutionResult.Succeeded(new TextExecutionOutput(reportJson));
        }

        private static string ExtractJsonValue(string json, string propertyName)
        {
            string marker = $"\"{propertyName}\": \"";
            int startIndex = json.IndexOf(marker, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                throw new InvalidOperationException($"Property {propertyName} was not found in {json}.");
            }

            startIndex += marker.Length;
            int endIndex = json.IndexOf('"', startIndex);
            return json[startIndex..endIndex];
        }

        private static (string FilePath, int FileSize, int FrameCount) ParseMetadata(string metadataJson)
        {
            string filePath = ExtractJsonValue(metadataJson, "filePath");
            int fileSize = ExtractJsonNumber(metadataJson, "fileSize");
            int frameCount = ExtractJsonNumber(metadataJson, "frameCount");
            return (filePath, fileSize, frameCount);
        }

        private static int ExtractJsonNumber(string json, string propertyName)
        {
            string marker = $"\"{propertyName}\": ";
            int startIndex = json.IndexOf(marker, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                throw new InvalidOperationException($"Property {propertyName} was not found in {json}.");
            }

            startIndex += marker.Length;
            int endIndex = json.IndexOfAny([',', '}'], startIndex);
            return int.Parse(json[startIndex..endIndex], System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}