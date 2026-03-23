namespace OrganizeMedia.Framework;

public sealed class DefaultTaskExecutor : ITaskExecutor
{
    public TaskExecutionResult Execute(IExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(executionContext);

        TaskSpecification specification = executionContext.TaskSpecification;
        ArgumentNullException.ThrowIfNull(specification);
        specification.Validate();

        string outputValue = specification.TaskType;

        if (!string.IsNullOrWhiteSpace(specification.InputJson))
        {
            outputValue = $"{specification.TaskType}({specification.InputType}): {specification.InputJson}";
        }
        return TaskExecutionResult.Succeeded(new TextExecutionOutput(outputValue));
    }
}