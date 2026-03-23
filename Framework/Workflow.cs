namespace OrganizeMedia.Framework;

public class Workflow
{
    private readonly TaskGraph _taskGraph;
    private readonly WorkflowExecutionState _executionState;
    private readonly Dictionary<TaskId, Task> _tasksById;
    private IWorkflowObserver _observer = NullWorkflowObserver.Instance;

    internal Workflow(WorkflowId workflowId)
        : this(workflowId, maxConcurrency: null)
    {
    }

    internal Workflow(WorkflowId workflowId, int? maxConcurrency)
    {
        WorkflowId = workflowId;
        MaxConcurrency = maxConcurrency;
        _taskGraph = new TaskGraph();
        _executionState = new WorkflowExecutionState(workflowId);
        _tasksById = new Dictionary<TaskId, Task>();
    }

    public WorkflowId WorkflowId { get; init; }

    public int? MaxConcurrency { get; }

    public static Workflow FromSpecification(WorkflowSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        specification.Validate();

        Workflow workflow = new Workflow(specification.WorkflowId, specification.MaxConcurrency);

        foreach (TaskSpecification taskSpecification in specification.Tasks)
        {
            Task task = new Task(specification.WorkflowId, taskSpecification);
            workflow.AddTask(task);
        }

        foreach (TaskDependencySpecification dependency in specification.Dependencies)
        {
            workflow.AddAdjacency(
                workflow._tasksById[dependency.PrerequisiteTaskId],
                workflow._tasksById[dependency.DependentTaskId]);
        }

        return workflow;
    }

    public WorkflowStatus Status => _executionState.ToStatus();

    private void MarkReadyToRun()
    {
        WorkflowStatus previousStatus = Status;
        ExecutionTransitionSupport.EnsureCanMarkReadyToRun(
            subjectName: "Workflow",
            subjectId: WorkflowId.Value,
            currentPhase: _executionState.ExecutionPhase,
            currentRecoverability: _executionState.Recoverability);

        _executionState.ExecutionOutcome = ExecutionOutcome.Pending;
        _executionState.FailureKind = ExecutionFailureKind.None;
        _executionState.Error = null;
        _executionState.Output = null;
        _executionState.ExecutionPhase = ExecutionPhase.ReadyToRun;
        NotifyTransition(previousStatus);
    }

    private void MarkQueued()
    {
        WorkflowStatus previousStatus = Status;
        ExecutionTransitionSupport.EnsurePhase(
            subjectName: "Workflow",
            subjectId: WorkflowId.Value,
            currentPhase: _executionState.ExecutionPhase,
            ExecutionPhase.ReadyToRun);
        _executionState.ExecutionPhase = ExecutionPhase.Queued;
        NotifyTransition(previousStatus);
    }

    private void MarkRunning()
    {
        WorkflowStatus previousStatus = Status;
        ExecutionTransitionSupport.EnsurePhase(
            subjectName: "Workflow",
            subjectId: WorkflowId.Value,
            currentPhase: _executionState.ExecutionPhase,
            ExecutionPhase.Queued);
        _executionState.ExecutionPhase = ExecutionPhase.Running;
        NotifyTransition(previousStatus);
    }

    internal void StartExecution()
    {
        MarkReadyToRun();
        MarkQueued();
        MarkRunning();
    }

    internal void MarkSucceeded(ExecutionOutput? output = null)
    {
        WorkflowStatus previousStatus = Status;
        ExecutionTransitionSupport.EnsurePhase(
            subjectName: "Workflow",
            subjectId: WorkflowId.Value,
            currentPhase: _executionState.ExecutionPhase,
            ExecutionPhase.Running);
        _executionState.ExecutionPhase = ExecutionPhase.Finished;
        _executionState.ExecutionOutcome = ExecutionOutcome.Succeeded;
        _executionState.FailureKind = ExecutionFailureKind.None;
        _executionState.Error = null;
        _executionState.Output = output;
        NotifyTransition(previousStatus);
    }

    internal void MarkCanceled(
        ExecutionOutput? output = null,
        ErrorInfo? error = null,
        ExecutionRecoverability recoverability = ExecutionRecoverability.Retryable)
    {
        WorkflowStatus previousStatus = Status;
        ExecutionTransitionSupport.EnsurePhase(
            subjectName: "Workflow",
            subjectId: WorkflowId.Value,
            currentPhase: _executionState.ExecutionPhase,
            ExecutionPhase.Running);
        ExecutionTransitionSupport.EnsureTerminalRecoverability(
            recoverability,
            nameof(recoverability),
            "Canceled workflows must use a terminal recoverability value.");

        _executionState.ExecutionPhase = ExecutionPhase.Finished;
        _executionState.ExecutionOutcome = ExecutionOutcome.Canceled;
        _executionState.FailureKind = ExecutionFailureKind.None;
        _executionState.Error = error;
        _executionState.Output = output;
        _executionState.Recoverability = recoverability;
        NotifyTransition(previousStatus);
    }

    internal void MarkFailed(
        ExecutionFailureKind failureKind,
        ExecutionOutput? output = null,
        ErrorInfo? error = null,
        ExecutionRecoverability? recoverability = null)
    {
        WorkflowStatus previousStatus = Status;
        ExecutionTransitionSupport.EnsurePhase(
            subjectName: "Workflow",
            subjectId: WorkflowId.Value,
            currentPhase: _executionState.ExecutionPhase,
            ExecutionPhase.Running);

        ExecutionRecoverability effectiveRecoverability = ExecutionTransitionSupport.ResolveFailureRecoverability(
            failureKind,
            recoverability,
            invalidFailureKindMessage: "Failed workflows must use a transient, permanent, or unknown failure kind.",
            invalidRecoverabilityMessage: "Failed workflows must use a terminal recoverability value.");

        _executionState.ExecutionPhase = ExecutionPhase.Finished;
        _executionState.ExecutionOutcome = ExecutionOutcome.Failed;
        _executionState.FailureKind = failureKind;
        _executionState.Error = error;
        _executionState.Output = output;
        _executionState.Recoverability = effectiveRecoverability;
        NotifyTransition(previousStatus);
    }

    internal void MarkFailed(
        Exception exception,
        ExecutionFailureKind failureKind,
        ExecutionOutput? output = null,
        ExecutionRecoverability? recoverability = null)
    {
        MarkFailed(
            output: output,
            error: ErrorInfo.FromException(exception),
            failureKind: failureKind,
            recoverability: recoverability);
    }

    internal void AddTask(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (_tasksById.ContainsKey(task.TaskId))
        {
            throw new InvalidOperationException($"Workflow {WorkflowId} already contains task {task.TaskId}.");
        }

        task.SetObserver(_observer);
        _taskGraph.AddTask(task);
        _tasksById.Add(task.TaskId, task);
        _executionState.AddTaskExecutionState(task.ExecutionState);
    }

    internal void AddAdjacency(Task task, Task adjacentTask)
    {
        _taskGraph.AddAdjacency(task, adjacentTask);
    }

    internal void ApplyRuntimeMutations(Task currentTask, TaskExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(currentTask);
        ArgumentNullException.ThrowIfNull(executionResult);

        TaskRuntimeMutations runtimeMutations = executionResult.RuntimeMutations ?? TaskRuntimeMutations.None;
        IReadOnlyCollection<TaskSpecification> spawnedTasks = runtimeMutations.SpawnedTasks;
        IReadOnlyCollection<TaskDependencySpecification> addedDependencies = runtimeMutations.AddedDependencies;
        IReadOnlyCollection<TaskFanInSpecification> fanInSpecifications = runtimeMutations.FanInSpecifications;

        List<TaskId> spawnedTaskIds = new List<TaskId>();

        foreach (TaskSpecification spawnedTaskSpecification in spawnedTasks)
        {
            TaskSpecification normalizedSpecification = spawnedTaskSpecification;

            if (normalizedSpecification.SpawnedByTaskId is null)
            {
                normalizedSpecification = normalizedSpecification with
                {
                    SpawnedByTaskId = currentTask.TaskId
                };
            }

            RuntimeAddTask(normalizedSpecification);
            spawnedTaskIds.Add(normalizedSpecification.TaskId);
        }

        foreach (TaskDependencySpecification dependency in addedDependencies)
        {
            RuntimeAddDependency(dependency);
        }

        foreach (TaskFanInSpecification fanInSpecification in fanInSpecifications)
        {
            RuntimeAddFanIn(fanInSpecification, spawnedTaskIds);
        }

    }

    internal Task RuntimeAddTask(TaskSpecification taskSpecification)
    {
        ArgumentNullException.ThrowIfNull(taskSpecification);
        taskSpecification.Validate();

        Task task = new Task(WorkflowId, taskSpecification);
        AddTask(task);
        NotifyTaskAdded(task);
        return task;
    }

    internal void RuntimeAddDependency(TaskDependencySpecification dependency)
    {
        if (!_tasksById.TryGetValue(dependency.PrerequisiteTaskId, out Task? prerequisiteTask))
        {
            throw new InvalidOperationException(
                $"Workflow {WorkflowId} references missing prerequisite task {dependency.PrerequisiteTaskId}.");
        }

        if (!_tasksById.TryGetValue(dependency.DependentTaskId, out Task? dependentTask))
        {
            throw new InvalidOperationException(
                $"Workflow {WorkflowId} references missing dependent task {dependency.DependentTaskId}.");
        }

        if (!HasNotStartedExecution(dependentTask))
        {
            throw new InvalidOperationException(
                $"Workflow {WorkflowId} can only add dependencies to tasks that have not started execution. Task {dependentTask.TaskId} is currently {dependentTask.Status.ExecutionPhase}.");
        }

        if (dependentTask.Status.ExecutionPhase != ExecutionPhase.NotStarted)
        {
            dependentTask.ResetToNotStarted();
        }

        AddAdjacency(prerequisiteTask, dependentTask);
        NotifyDependencyAdded(dependency);
    }

    private static bool HasNotStartedExecution(Task task)
    {
        return ExecutionTransitionSupport.HasNotStartedExecution(task.Status.ExecutionPhase);
    }

    internal void RuntimeAddFanIn(TaskFanInSpecification fanInSpecification, IReadOnlyCollection<TaskId> spawnedTaskIds)
    {
        ArgumentNullException.ThrowIfNull(fanInSpecification);
        fanInSpecification.Validate();

        HashSet<TaskId> prerequisiteTaskIds = new HashSet<TaskId>();

        if (fanInSpecification.IncludeSpawnedTasks && spawnedTaskIds is not null)
        {
            foreach (TaskId spawnedTaskId in spawnedTaskIds)
            {
                prerequisiteTaskIds.Add(spawnedTaskId);
            }
        }

        if (fanInSpecification.AdditionalPrerequisiteTaskIds is not null)
        {
            foreach (TaskId prerequisiteTaskId in fanInSpecification.AdditionalPrerequisiteTaskIds)
            {
                prerequisiteTaskIds.Add(prerequisiteTaskId);
            }
        }

        foreach (TaskId prerequisiteTaskId in prerequisiteTaskIds)
        {
            RuntimeAddDependency(new TaskDependencySpecification(
                PrerequisiteTaskId: prerequisiteTaskId,
                DependentTaskId: fanInSpecification.JoinTaskId));
        }

        NotifyFanInExpanded(fanInSpecification.JoinTaskId, prerequisiteTaskIds.ToArray());
    }

    internal IReadOnlyCollection<Task> GetTasks()
    {
        return _taskGraph.GetTasks();
    }

    internal bool TryGetTask(TaskId taskId, out Task? task)
    {
        return _tasksById.TryGetValue(taskId, out task);
    }

    internal IReadOnlyCollection<Task> GetDependencies(Task task)
    {
        return _taskGraph.GetDependencies(task);
    }

    public void RunToCompletion(ITaskExecutor? taskExecutor = null, IWorkflowObserver? observer = null)
    {
        if (observer is not null)
        {
            SetObserver(observer);
        }

        WorkflowOrchestrator orchestrator = new WorkflowOrchestrator(taskExecutor);
        orchestrator.RunToCompletion(this);
    }

    public void SetObserver(IWorkflowObserver? observer)
    {
        _observer = observer ?? NullWorkflowObserver.Instance;

        foreach (Task task in _tasksById.Values)
        {
            task.SetObserver(_observer);
        }
    }

    private void NotifyTransition(WorkflowStatus previousStatus)
    {
        WorkflowStatus currentStatus = Status;

        try
        {
            _observer.OnWorkflowTransition(new WorkflowTransitionEvent(
                WorkflowId: WorkflowId,
                PreviousStatus: previousStatus,
                CurrentStatus: currentStatus,
                Timestamp: currentStatus.Timestamp ?? DateTime.UtcNow));
        }
        catch
        {
        }
    }

    private void NotifyTaskAdded(Task task)
    {
        try
        {
            _observer.OnTaskAdded(new TaskAddedEvent(
                WorkflowId: WorkflowId,
                TaskId: task.TaskId,
                TaskSpecification: task.Specification,
                TaskStatus: task.Status,
                Timestamp: task.Status.Timestamp ?? DateTime.UtcNow));
        }
        catch
        {
        }
    }

    private void NotifyDependencyAdded(TaskDependencySpecification dependency)
    {
        try
        {
            _observer.OnDependencyAdded(new DependencyAddedEvent(
                WorkflowId: WorkflowId,
                PrerequisiteTaskId: dependency.PrerequisiteTaskId,
                DependentTaskId: dependency.DependentTaskId,
                Timestamp: DateTime.UtcNow));
        }
        catch
        {
        }
    }

    private void NotifyFanInExpanded(TaskId joinTaskId, IReadOnlyCollection<TaskId> prerequisiteTaskIds)
    {
        try
        {
            _observer.OnFanInExpanded(new FanInExpandedEvent(
                WorkflowId: WorkflowId,
                JoinTaskId: joinTaskId,
                PrerequisiteTaskIds: prerequisiteTaskIds,
                Timestamp: DateTime.UtcNow));
        }
        catch
        {
        }
    }
}
