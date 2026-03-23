namespace OrganizeMedia.Tests;

using OrganizeMedia.Framework;

public class DefaultTaskExecutorTests
{
    [Fact]
    public void Execute_FormatsOutputUsingTaskTypeWhenNoInputJsonIsPresent()
    {
        DefaultTaskExecutor executor = new DefaultTaskExecutor();
        IExecutionContext executionContext = new FakeExecutionContext(
            new TaskSpecification(new TaskTemplateId("A"), new TaskType("ScanDirectory")));

        TaskExecutionResult result = executor.Execute(executionContext);

        Assert.Equal(ExecutionOutcome.Succeeded, result.ExecutionOutcome);
        Assert.Equal("ScanDirectory", Assert.IsType<TextExecutionOutput>(result.Output).Value);
    }

    [Fact]
    public void Execute_FormatsOutputUsingDeclaredInputTypeWhenInputJsonIsPresent()
    {
        DefaultTaskExecutor executor = new DefaultTaskExecutor();
        IExecutionContext executionContext = new FakeExecutionContext(
            new TaskSpecification(
                TaskTemplateId: new TaskTemplateId("A"),
                TaskType: new TaskType("ScanDirectory"),
                InputType: new InputType("application/json"),
                InputJson: "{ \"path\": \"c:/media\" }"));

        TaskExecutionResult result = executor.Execute(executionContext);

        Assert.Equal(ExecutionOutcome.Succeeded, result.ExecutionOutcome);
        Assert.Equal(
            "ScanDirectory(application/json): { \"path\": \"c:/media\" }",
            Assert.IsType<TextExecutionOutput>(result.Output).Value);
    }

    [Fact]
    public void Execute_ThrowsWhenTaskSpecificationIsInvalid()
    {
        DefaultTaskExecutor executor = new DefaultTaskExecutor();
        IExecutionContext executionContext = new FakeExecutionContext(
            new TaskSpecification(
                TaskTemplateId: new TaskTemplateId("A"),
                TaskType: new TaskType("ScanDirectory"),
                InputJson: "{ \"path\": \"c:/media\" }"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => executor.Execute(executionContext));

        Assert.Contains("must provide InputType when InputJson is present", exception.Message);
    }

    private sealed class FakeExecutionContext : IExecutionContext
    {
        public FakeExecutionContext(TaskSpecification taskSpecification)
        {
            WorkflowTemplateId = new WorkflowTemplateId("W0");
            WorkflowInstanceId = new WorkflowInstanceId(WorkflowTemplateId, 1);
            TaskTemplateId = taskSpecification.TaskTemplateId;
            TaskInstanceId = new TaskInstanceId(taskSpecification.TaskTemplateId, 1);
            TaskSpecification = taskSpecification;
            DependencyStatuses = new Dictionary<TaskInstanceId, TaskStatus>();
            DependencyOutputs = new Dictionary<TaskInstanceId, ExecutionOutput?>();
        }

        public WorkflowTemplateId WorkflowTemplateId { get; }

        public WorkflowInstanceId WorkflowInstanceId { get; }

        public TaskTemplateId TaskTemplateId { get; }

        public TaskInstanceId TaskInstanceId { get; }

        public TaskInstanceId? SpawnedByTaskInstanceId { get; }

        public TaskSpecification TaskSpecification { get; }

        public IReadOnlyDictionary<TaskInstanceId, TaskStatus> DependencyStatuses { get; }

        public IReadOnlyDictionary<TaskInstanceId, ExecutionOutput?> DependencyOutputs { get; }
    }
}