namespace OrganizeMedia.Framework;

public class Workflow
{
    TaskGraph _taskGraph = null;
    WorkflowExecutionState _executionState = null;
    Dictionary<TaskId, Task> _tasksById = null;
    private IWorkflowObserver _observer = NullWorkflowObserver.Instance;

    public Workflow(WorkflowId workflowId)
        : this(workflowId, maxConcurrency: null)
    {
    }

    public Workflow(WorkflowId workflowId, int? maxConcurrency)
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
        Dictionary<TaskId, Task> tasksById = new Dictionary<TaskId, Task>();

        foreach (TaskSpecification taskSpecification in specification.Tasks)
        {
            Task task = new Task(specification.WorkflowId, taskSpecification);
            workflow.AddTask(task);
            tasksById.Add(task.TaskId, task);
        }

        foreach (TaskDependencySpecification dependency in specification.Dependencies)
        {
            if (!tasksById.TryGetValue(dependency.PrerequisiteTaskId, out Task prerequisiteTask))
            {
                throw new InvalidOperationException(
                    $"Workflow specification {specification.WorkflowId} references missing prerequisite task {dependency.PrerequisiteTaskId}.");
            }

            if (!tasksById.TryGetValue(dependency.DependentTaskId, out Task dependentTask))
            {
                throw new InvalidOperationException(
                    $"Workflow specification {specification.WorkflowId} references missing dependent task {dependency.DependentTaskId}.");
            }

            workflow.AddAdjacency(prerequisiteTask, dependentTask);
        }

        return workflow;
    }

    public WorkflowStatus Status => _executionState.ToStatus();

    public string ToDisplayString()
    {
        var status = Status;
        return $"/{WorkflowId} {status.ExecutionPhase}, {status.ExecutionOutcome}, {status.FailureKind}, {status.Recoverability}";
    }

    private static bool IsRestartRecoverability(ExecutionRecoverability recoverability)
    {
        return recoverability == ExecutionRecoverability.Retryable ||
               recoverability == ExecutionRecoverability.Resumable;
    }

    private static bool IsTerminalRecoverability(ExecutionRecoverability recoverability)
    {
        return recoverability == ExecutionRecoverability.Retryable ||
               recoverability == ExecutionRecoverability.Resumable ||
               recoverability == ExecutionRecoverability.RequiresIntervention ||
               recoverability == ExecutionRecoverability.NotRecoverable;
    }

    private void EnsurePhase(params ExecutionPhase[] allowedPhases)
    {
        if (allowedPhases.Contains(_executionState.ExecutionPhase))
            return;

        throw new InvalidOperationException(
            $"Workflow {WorkflowId} cannot transition from {_executionState.ExecutionPhase} to the requested state.");
    }

    public void MarkReadyToRun()
    {
        WorkflowStatus previousStatus = Status;
        ExecutionPhase currentPhase = _executionState.ExecutionPhase;
        ExecutionRecoverability currentRecoverability = _executionState.Recoverability;

        if (currentPhase != ExecutionPhase.NotStarted &&
            !(currentPhase == ExecutionPhase.Finished && IsRestartRecoverability(currentRecoverability)))
        {
            throw new InvalidOperationException(
                $"Workflow {WorkflowId} can only become ready-to-run from NotStarted or a retryable finished state.");
        }

        _executionState.ExecutionOutcome = ExecutionOutcome.Pending;
        _executionState.FailureKind = ExecutionFailureKind.None;
        _executionState.Error = null;
        _executionState.Output = null;
        _executionState.ExecutionPhase = ExecutionPhase.ReadyToRun;
        NotifyTransition(previousStatus);
    }

    public void MarkQueued()
    {
        WorkflowStatus previousStatus = Status;
        EnsurePhase(ExecutionPhase.ReadyToRun);
        _executionState.ExecutionPhase = ExecutionPhase.Queued;
        NotifyTransition(previousStatus);
    }

    public void MarkRunning()
    {
        WorkflowStatus previousStatus = Status;
        EnsurePhase(ExecutionPhase.Queued);
        _executionState.ExecutionPhase = ExecutionPhase.Running;
        NotifyTransition(previousStatus);
    }

    public void MarkSucceeded(ExecutionOutput output = null)
    {
        WorkflowStatus previousStatus = Status;
        EnsurePhase(ExecutionPhase.Running);
        _executionState.ExecutionPhase = ExecutionPhase.Finished;
        _executionState.ExecutionOutcome = ExecutionOutcome.Succeeded;
        _executionState.FailureKind = ExecutionFailureKind.None;
        _executionState.Error = null;
        _executionState.Output = output;
        NotifyTransition(previousStatus);
    }

    public void MarkCanceled(
        ExecutionOutput output = null,
        ErrorInfo error = null,
        ExecutionRecoverability recoverability = ExecutionRecoverability.Retryable)
    {
        WorkflowStatus previousStatus = Status;
        EnsurePhase(ExecutionPhase.Running);

        if (!IsTerminalRecoverability(recoverability))
        {
            throw new ArgumentOutOfRangeException(
                nameof(recoverability),
                recoverability,
                "Canceled workflows must use a terminal recoverability value.");
        }

        _executionState.ExecutionPhase = ExecutionPhase.Finished;
        _executionState.ExecutionOutcome = ExecutionOutcome.Canceled;
        _executionState.FailureKind = ExecutionFailureKind.None;
        _executionState.Error = error;
        _executionState.Output = output;
        _executionState.Recoverability = recoverability;
        NotifyTransition(previousStatus);
    }

    public void MarkFailed(
        ExecutionFailureKind failureKind,
        ExecutionOutput output = null,
        ErrorInfo error = null,
        ExecutionRecoverability? recoverability = null)
    {
        WorkflowStatus previousStatus = Status;
        EnsurePhase(ExecutionPhase.Running);

        if (failureKind == ExecutionFailureKind.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "Failed workflows must use a transient, permanent, or unknown failure kind.");
        }

        ExecutionRecoverability effectiveRecoverability = recoverability ??
            ExecutionRecoverabilityDefaults.From(
            ExecutionPhase.Finished,
            ExecutionOutcome.Failed,
            failureKind);

        if (!IsTerminalRecoverability(effectiveRecoverability))
        {
            throw new ArgumentOutOfRangeException(
                nameof(recoverability),
                effectiveRecoverability,
                "Failed workflows must use a terminal recoverability value.");
        }

        _executionState.ExecutionPhase = ExecutionPhase.Finished;
        _executionState.ExecutionOutcome = ExecutionOutcome.Failed;
        _executionState.FailureKind = failureKind;
        _executionState.Error = error;
        _executionState.Output = output;
        _executionState.Recoverability = effectiveRecoverability;
        NotifyTransition(previousStatus);
    }

    public void MarkFailed(
        Exception exception,
        ExecutionFailureKind failureKind,
        ExecutionOutput output = null,
        ExecutionRecoverability? recoverability = null)
    {
        MarkFailed(
            output: output,
            error: ErrorInfo.FromException(exception),
            failureKind: failureKind,
            recoverability: recoverability);
    }

    public void AddTask(Task task)
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

    public void AddAdjacency(Task task, Task adjacentTask)
    {
        _taskGraph.AddAdjacency(task, adjacentTask);
    }

    public Task RuntimeAddTask(TaskSpecification taskSpecification)
    {
        ArgumentNullException.ThrowIfNull(taskSpecification);
        taskSpecification.Validate();

        Task task = new Task(WorkflowId, taskSpecification);
        AddTask(task);
        NotifyTaskAdded(task);
        return task;
    }

    public void RuntimeAddDependency(TaskDependencySpecification dependency)
    {
        if (!_tasksById.TryGetValue(dependency.PrerequisiteTaskId, out Task prerequisiteTask))
        {
            throw new InvalidOperationException(
                $"Workflow {WorkflowId} references missing prerequisite task {dependency.PrerequisiteTaskId}.");
        }

        if (!_tasksById.TryGetValue(dependency.DependentTaskId, out Task dependentTask))
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
        return task.Status.ExecutionPhase == ExecutionPhase.NotStarted ||
               task.Status.ExecutionPhase == ExecutionPhase.ReadyToRun ||
               task.Status.ExecutionPhase == ExecutionPhase.Queued;
    }

    public void RuntimeAddFanIn(TaskFanInSpecification fanInSpecification, IReadOnlyCollection<TaskId> spawnedTaskIds)
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

    public TaskList TopologicalSort()
    {
        return _taskGraph.TopologicalSort();
    }

    internal TaskList GetTasks()
    {
        return _taskGraph.GetTasks();
    }

    internal bool TryGetTask(TaskId taskId, out Task task)
    {
        return _tasksById.TryGetValue(taskId, out task);
    }

    internal TaskList GetAdjacencies(Task task)
    {
        return _taskGraph.GetAdjacencies(task);
    }

    internal TaskList GetDependencies(Task task)
    {
        return _taskGraph.GetDependencies(task);
    }

    public void RunToCompletion(ITaskExecutor taskExecutor = null, IWorkflowObserver observer = null)
    {
        if (observer is not null)
        {
            SetObserver(observer);
        }

        WorkflowOrchestrator orchestrator = new WorkflowOrchestrator(taskExecutor);
        orchestrator.RunToCompletion(this);
    }

    public void SetObserver(IWorkflowObserver observer)
    {
        _observer = observer ?? NullWorkflowObserver.Instance;

        foreach (Task task in _tasksById.Values)
        {
            task.SetObserver(_observer);
        }
    }

    public string ToGraphDisplayString()
    {
        TaskListDictionary adjacencyList = _taskGraph.GetAdjacencyList();
        TaskListDictionary dependencyList = _taskGraph.GetDependencyList();
        TaskList tasks = TopologicalSort();

        return string.Join(
            Environment.NewLine,
            [
                "Adjacency list:",
                adjacencyList.ToDisplayString(),
                "Dependency list:",
                dependencyList.ToDisplayString(),
                "Topological order:",
                tasks.ToDisplayString()
            ]);
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
