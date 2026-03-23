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
        _writer.WriteLine(ExecutionDisplayFormatter.FormatTaskStatus(
            transitionEvent.WorkflowId,
            transitionEvent.TaskId,
            transitionEvent.CurrentStatus));
    }

    public void OnWorkflowTransition(WorkflowTransitionEvent transitionEvent)
    {
        ArgumentNullException.ThrowIfNull(transitionEvent);
        _writer.WriteLine(ExecutionDisplayFormatter.FormatWorkflowStatus(
            transitionEvent.WorkflowId,
            transitionEvent.CurrentStatus));
    }

    public void OnTaskAdded(TaskAddedEvent taskAddedEvent)
    {
        ArgumentNullException.ThrowIfNull(taskAddedEvent);
        _writer.WriteLine($"/{taskAddedEvent.WorkflowId}/{taskAddedEvent.TaskId} added by runtime mutation");
    }

    public void OnDependencyAdded(DependencyAddedEvent dependencyAddedEvent)
    {
        ArgumentNullException.ThrowIfNull(dependencyAddedEvent);
        _writer.WriteLine(
            $"/{dependencyAddedEvent.WorkflowId} dependency added: {dependencyAddedEvent.PrerequisiteTaskId} -> {dependencyAddedEvent.DependentTaskId}");
    }

    public void OnFanInExpanded(FanInExpandedEvent fanInExpandedEvent)
    {
        ArgumentNullException.ThrowIfNull(fanInExpandedEvent);
        _writer.WriteLine(
            $"/{fanInExpandedEvent.WorkflowId} fan-in expanded for {fanInExpandedEvent.JoinTaskId}: [{string.Join(",", fanInExpandedEvent.PrerequisiteTaskIds)}]");
    }
}