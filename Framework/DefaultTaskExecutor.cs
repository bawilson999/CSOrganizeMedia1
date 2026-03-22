namespace OrganizeMedia.Framework;

public class DefaultTaskExecutor : ITaskExecutor
{
    public TaskExecutionResult Execute(IExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(executionContext);

        TaskSpecification specification = executionContext.TaskSpecification;
        string outputValue = specification.TaskType;

        if (!string.IsNullOrWhiteSpace(specification.InputJson))
        {
            string inputType = specification.InputType ?? "json";
            outputValue = $"{specification.TaskType}({inputType}): {specification.InputJson}";
        }

        return TaskExecutionResult.Succeeded(new TextExecutionOutput(outputValue));
    }
}