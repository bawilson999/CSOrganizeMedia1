namespace OrganizeMedia.Tests;

using OrganizeMedia.Framework;

public class DefaultTaskExecutorTests
{
    [Fact]
    public void Execute_FormatsOutputUsingTaskTypeWhenNoInputJsonIsPresent()
    {
        DefaultTaskExecutor executor = new DefaultTaskExecutor();
        IExecutionContext executionContext = new FakeExecutionContext(
            new TaskSpecification(new TaskId("A"), new TaskType("ScanDirectory")));

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
                TaskId: new TaskId("A"),
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
                TaskId: new TaskId("A"),
                TaskType: new TaskType("ScanDirectory"),
                InputJson: "{ \"path\": \"c:/media\" }"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => executor.Execute(executionContext));

        Assert.Contains("must provide InputType when InputJson is present", exception.Message);
    }

    private sealed class FakeExecutionContext : IExecutionContext
    {
        public FakeExecutionContext(TaskSpecification taskSpecification)
        {
            WorkflowId = new WorkflowId("W0");
            TaskId = taskSpecification.TaskId;
            TaskSpecification = taskSpecification;
            DependencyStatuses = new Dictionary<TaskId, TaskStatus>();
            DependencyOutputs = new Dictionary<TaskId, ExecutionOutput?>();
        }

        public WorkflowId WorkflowId { get; }

        public TaskId TaskId { get; }

        public TaskSpecification TaskSpecification { get; }

        public IReadOnlyDictionary<TaskId, TaskStatus> DependencyStatuses { get; }

        public IReadOnlyDictionary<TaskId, ExecutionOutput?> DependencyOutputs { get; }
    }
}