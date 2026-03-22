namespace OrganizeMedia.Framework;

public interface ITaskExecutor
{
    TaskExecutionResult Execute(IExecutionContext executionContext);
}