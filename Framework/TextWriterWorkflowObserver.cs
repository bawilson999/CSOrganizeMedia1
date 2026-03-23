using System.IO;

namespace OrganizeMedia.Framework;

public sealed class TextWriterWorkflowObserver : IWorkflowObserver
{
    private readonly TextWriter _writer;

    public TextWriterWorkflowObserver(TextWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public void OnTaskTransition(TaskTransitionEvent transitionEvent)
    {
        ArgumentNullException.ThrowIfNull(transitionEvent);
        _writer.WriteLine(TaskStatusFormatter.Format(
            transitionEvent.WorkflowId,
            transitionEvent.TaskInstanceId,
            transitionEvent.CurrentStatus));
    }

    public void OnWorkflowTransition(WorkflowTransitionEvent transitionEvent)
    {
        ArgumentNullException.ThrowIfNull(transitionEvent);
        _writer.WriteLine(WorkflowStatusFormatter.Format(
            transitionEvent.WorkflowId,
            transitionEvent.CurrentStatus));
    }

    public void OnTaskAdded(TaskAddedEvent taskAddedEvent)
    {
        ArgumentNullException.ThrowIfNull(taskAddedEvent);
        _writer.WriteLine($"/{taskAddedEvent.WorkflowId}/{taskAddedEvent.TaskInstanceId} added by runtime graph change");
    }

    public void OnDependencyAdded(DependencyAddedEvent dependencyAddedEvent)
    {
        ArgumentNullException.ThrowIfNull(dependencyAddedEvent);
        _writer.WriteLine(
            $"/{dependencyAddedEvent.WorkflowId} dependency added: {dependencyAddedEvent.PrerequisiteTaskInstanceId} -> {dependencyAddedEvent.DependentTaskInstanceId}");
    }
}