namespace OrganizeMedia.Framework;

public interface IWorkflowObserver
{
    void OnTaskTransition(TaskTransitionEvent transitionEvent);

    void OnWorkflowTransition(WorkflowTransitionEvent transitionEvent);

    void OnTaskAdded(TaskAddedEvent taskAddedEvent);

    void OnDependencyAdded(DependencyAddedEvent dependencyAddedEvent);

    void OnFanInExpanded(FanInExpandedEvent fanInExpandedEvent);
}

internal sealed class NullWorkflowObserver : IWorkflowObserver
{
    internal static NullWorkflowObserver Instance { get; } = new NullWorkflowObserver();

    private NullWorkflowObserver()
    {
    }

    public void OnTaskTransition(TaskTransitionEvent transitionEvent)
    {
    }

    public void OnWorkflowTransition(WorkflowTransitionEvent transitionEvent)
    {
    }

    public void OnTaskAdded(TaskAddedEvent taskAddedEvent)
    {
    }

    public void OnDependencyAdded(DependencyAddedEvent dependencyAddedEvent)
    {
    }

    public void OnFanInExpanded(FanInExpandedEvent fanInExpandedEvent)
    {
    }
}