namespace OrganizeMedia.Framework;

public class Workflow
{
    private readonly TaskGraph _taskGraph;
    private readonly WorkflowExecutionState _executionState;
    private readonly Dictionary<TaskInstanceId, Task> _tasksByInstanceId;
    private readonly Dictionary<TaskSpecificationId, List<Task>> _tasksBySpecificationId;
    private readonly Dictionary<TaskSpecificationId, int> _nextInstanceNumberByTaskSpecificationId;
    private readonly Dictionary<TaskSpecificationId, TaskSpecification> _taskSpecificationsById;
    private IWorkflowObserver _observer = NullWorkflowObserver.Instance;

    internal Workflow(WorkflowSpecificationId workflowSpecificationId, int? maxConcurrency)
    {
        WorkflowSpecificationId = workflowSpecificationId;
        WorkflowInstanceId = new WorkflowInstanceId(workflowSpecificationId, 1);
        MaxConcurrency = maxConcurrency;
        _taskGraph = new TaskGraph();
        _executionState = new WorkflowExecutionState(WorkflowInstanceId);
        _tasksByInstanceId = new Dictionary<TaskInstanceId, Task>();
        _tasksBySpecificationId = new Dictionary<TaskSpecificationId, List<Task>>();
        _nextInstanceNumberByTaskSpecificationId = new Dictionary<TaskSpecificationId, int>();
        _taskSpecificationsById = new Dictionary<TaskSpecificationId, TaskSpecification>();
    }

    public WorkflowSpecificationId WorkflowSpecificationId { get; init; }

    public WorkflowInstanceId WorkflowInstanceId { get; init; }

    public int? MaxConcurrency { get; }

    public static Workflow FromSpecification(WorkflowSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        specification.Validate();

        Workflow workflow = new Workflow(specification.WorkflowSpecificationId, specification.MaxConcurrency);

        foreach (TaskSpecification taskSpecification in specification.Tasks)
        {
            workflow.RegisterTaskSpecification(taskSpecification);

            for (int instanceIndex = 0; instanceIndex < taskSpecification.InitialInstanceCount; instanceIndex++)
            {
                Task task = workflow.CreateTask(taskSpecification);
                workflow.AddTask(task);
            }
        }

        foreach (TaskDependencySpecification dependency in specification.Dependencies)
        {
            if (workflow.TryGetSingleSpecificationTask(dependency.PrerequisiteTaskSpecificationId, out Task? prerequisiteTask) &&
                workflow.TryGetSingleSpecificationTask(dependency.DependentTaskSpecificationId, out Task? dependentTask))
            {
                workflow.AddAdjacency(prerequisiteTask!, dependentTask!);
            }
        }

        return workflow;
    }

    public WorkflowStatus Status => _executionState.ToStatus();

    private void MarkReadyToRun()
    {
        WorkflowStatus previousStatus = Status;
        ExecutionTransitionSupport.EnsureCanMarkReadyToRun(
            subjectName: "Workflow",
            subjectId: WorkflowInstanceId.ToString(),
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
            subjectId: WorkflowInstanceId.ToString(),
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
            subjectId: WorkflowInstanceId.ToString(),
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
            subjectId: WorkflowInstanceId.ToString(),
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
            subjectId: WorkflowInstanceId.ToString(),
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
            subjectId: WorkflowInstanceId.ToString(),
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

        if (_tasksByInstanceId.ContainsKey(task.TaskInstanceId))
        {
            throw new InvalidOperationException($"Workflow {WorkflowInstanceId} already contains task instance {task.TaskInstanceId}.");
        }

        task.SetObserver(_observer);
        _taskGraph.AddTask(task);
        _tasksByInstanceId.Add(task.TaskInstanceId, task);
        if (!_tasksBySpecificationId.TryGetValue(task.TaskSpecificationId, out List<Task>? taskInstances))
        {
            taskInstances = new List<Task>();
            _tasksBySpecificationId.Add(task.TaskSpecificationId, taskInstances);
        }

        taskInstances.Add(task);
        _executionState.AddTaskExecutionState(task.ExecutionState);
    }

    private void RegisterTaskSpecification(TaskSpecification taskSpecification)
    {
        _taskSpecificationsById.Add(taskSpecification.TaskSpecificationId, taskSpecification);
    }

    private Task CreateTask(TaskSpecification taskSpecification, TaskInstanceId? spawnedByTaskInstanceId = null)
    {
        TaskSpecificationId taskSpecificationId = taskSpecification.TaskSpecificationId;
        int nextInstanceNumber = _nextInstanceNumberByTaskSpecificationId.TryGetValue(taskSpecificationId, out int currentInstanceNumber)
            ? currentInstanceNumber + 1
            : 1;

        _nextInstanceNumberByTaskSpecificationId[taskSpecificationId] = nextInstanceNumber;
        return new Task(
            WorkflowInstanceId,
            taskSpecification,
            new TaskInstanceId(taskSpecificationId, nextInstanceNumber),
            spawnedByTaskInstanceId);
    }

    private Task GetRequiredSingleSpecificationTask(TaskSpecificationId taskSpecificationId)
    {
        if (!_tasksBySpecificationId.TryGetValue(taskSpecificationId, out List<Task>? taskInstances) || taskInstances.Count == 0)
        {
            throw new InvalidOperationException($"Workflow {WorkflowInstanceId} references missing task specification {taskSpecificationId}.");
        }

        if (taskInstances.Count != 1)
        {
            throw new InvalidOperationException(
                $"Workflow {WorkflowInstanceId} cannot resolve specification task {taskSpecificationId} to a single runtime instance because {taskInstances.Count} instances exist.");
        }

        return taskInstances[0];
    }

    private bool TryGetSingleSpecificationTask(TaskSpecificationId taskSpecificationId, out Task? task)
    {
        task = null;

        if (!_tasksBySpecificationId.TryGetValue(taskSpecificationId, out List<Task>? taskInstances) || taskInstances.Count == 0)
        {
            return false;
        }

        if (taskInstances.Count != 1)
        {
            throw new InvalidOperationException(
                $"Workflow {WorkflowInstanceId} cannot resolve specification task {taskSpecificationId} to a single runtime instance because {taskInstances.Count} instances exist.");
        }

        task = taskInstances[0];
        return true;
    }

    internal void AddAdjacency(Task task, Task adjacentTask)
    {
        _taskGraph.AddAdjacency(task, adjacentTask);
    }

    internal void ApplyGraphChanges(Task currentTask, TaskExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(currentTask);
        ArgumentNullException.ThrowIfNull(executionResult);

        TaskGraphChanges graphChanges = executionResult.GraphChanges;
        IReadOnlyCollection<TaskSpecification> spawnedTasks = graphChanges.SpawnedTasks;
        IReadOnlyCollection<TaskDependencySpecification> addedDependencies = graphChanges.AddedDependencies;
        IReadOnlyCollection<TaskSpecificationSpawn> spawnedTaskSpecifications = graphChanges.SpawnedTaskSpecifications;
        IReadOnlyCollection<TaskInstanceDependency> addedInstanceDependencies = graphChanges.AddedInstanceDependencies;
        Dictionary<string, Task> spawnedTasksByKey = new Dictionary<string, Task>(StringComparer.Ordinal);

        foreach (TaskSpecification spawnedTaskSpecification in spawnedTasks)
        {
            RuntimeAddTask(spawnedTaskSpecification, currentTask.TaskInstanceId);
        }

        foreach (TaskSpecificationSpawn taskSpecificationSpawn in spawnedTaskSpecifications)
        {
            if (string.IsNullOrWhiteSpace(taskSpecificationSpawn.SpawnKey))
            {
                throw new InvalidOperationException("Spawned task specifications must provide a non-empty SpawnKey.");
            }

            Task spawnedTask = RuntimeSpawnTaskFromSpecification(currentTask, taskSpecificationSpawn);

            if (!spawnedTasksByKey.TryAdd(taskSpecificationSpawn.SpawnKey, spawnedTask))
            {
                throw new InvalidOperationException(
                    $"Workflow {WorkflowInstanceId} received duplicate spawned task key {taskSpecificationSpawn.SpawnKey} in one graph change.");
            }
        }

        foreach (TaskDependencySpecification dependency in addedDependencies)
        {
            RuntimeAddDependency(dependency);
        }

        foreach (TaskInstanceDependency dependency in addedInstanceDependencies)
        {
            Task prerequisiteTask = ResolveTaskNodeReference(dependency.Prerequisite, spawnedTasksByKey);
            Task dependentTask = ResolveTaskNodeReference(dependency.Dependent, spawnedTasksByKey);
            RuntimeAddDependency(prerequisiteTask, dependentTask);
        }
    }

    internal Task RuntimeAddTask(TaskSpecification taskSpecification, TaskInstanceId? spawnedByTaskInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(taskSpecification);
        taskSpecification.Validate();

        Task task = CreateTask(taskSpecification, spawnedByTaskInstanceId);
        AddTask(task);
        NotifyTaskAdded(task);
        return task;
    }

    private Task RuntimeSpawnTaskFromSpecification(Task currentTask, TaskSpecificationSpawn taskSpecificationSpawn)
    {
        if (!_taskSpecificationsById.TryGetValue(taskSpecificationSpawn.TaskSpecificationId, out TaskSpecification? taskSpecification))
        {
            throw new InvalidOperationException(
            $"Workflow {WorkflowInstanceId} references missing task specification {taskSpecificationSpawn.TaskSpecificationId}.");
        }

        InputType? effectiveInputType = taskSpecificationSpawn.InputType ?? taskSpecification.InputType;
        string? effectiveInputJson = taskSpecificationSpawn.InputJson ?? taskSpecification.InputJson;

        TaskSpecification instanceSpecification = taskSpecification with
        {
            Cardinality = TaskCardinality.Singleton,
            InputType = effectiveInputType,
            InputJson = effectiveInputJson,
            InitialInstanceCount = 1
        };

        return RuntimeAddTask(instanceSpecification, currentTask.TaskInstanceId);
    }

    internal void RuntimeAddDependency(TaskDependencySpecification dependency)
    {
        Task prerequisiteTask = GetRequiredSingleSpecificationTask(dependency.PrerequisiteTaskSpecificationId);
        Task dependentTask = GetRequiredSingleSpecificationTask(dependency.DependentTaskSpecificationId);

        RuntimeAddDependency(prerequisiteTask, dependentTask);
    }

    private void RuntimeAddDependency(Task prerequisiteTask, Task dependentTask)
    {
        ArgumentNullException.ThrowIfNull(prerequisiteTask);
        ArgumentNullException.ThrowIfNull(dependentTask);

        if (!ExecutionTransitionSupport.HasNotStartedExecution(dependentTask.Status.ExecutionPhase))
        {
            throw new InvalidOperationException(
                $"Workflow {WorkflowInstanceId} can only add dependencies to tasks that have not started execution. Task {dependentTask.TaskInstanceId} is currently {dependentTask.Status.ExecutionPhase}.");
        }

        if (dependentTask.Status.ExecutionPhase != ExecutionPhase.NotStarted)
        {
            dependentTask.ResetToNotStarted();
        }

        AddAdjacency(prerequisiteTask, dependentTask);
        NotifyDependencyAdded(prerequisiteTask, dependentTask);
    }

    private Task ResolveTaskNodeReference(TaskNodeReference reference, IReadOnlyDictionary<string, Task> spawnedTasksByKey)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (reference.TaskInstanceId is not null)
        {
            if (!_tasksByInstanceId.TryGetValue(reference.TaskInstanceId.Value, out Task? task))
            {
                throw new InvalidOperationException(
                    $"Workflow {WorkflowInstanceId} references missing task instance {reference.TaskInstanceId.Value}.");
            }

            return task;
        }

        if (reference.TaskSpecificationId is not null)
        {
            return GetRequiredSingleSpecificationTask(reference.TaskSpecificationId.Value);
        }

        if (reference.SpawnKey is not null && spawnedTasksByKey.TryGetValue(reference.SpawnKey, out Task? spawnedTask))
        {
            return spawnedTask;
        }

        throw new InvalidOperationException(
            $"Workflow {WorkflowInstanceId} references missing spawned task key {reference.SpawnKey}.");
    }

    internal IReadOnlyCollection<Task> GetTasks()
    {
        return _taskGraph.GetTasks();
    }

    internal bool TryGetTask(TaskInstanceId taskInstanceId, out Task? task)
    {
        return _tasksByInstanceId.TryGetValue(taskInstanceId, out task);
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

        foreach (Task task in _tasksByInstanceId.Values)
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
                WorkflowSpecificationId: WorkflowSpecificationId,
                WorkflowInstanceId: WorkflowInstanceId,
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
                WorkflowSpecificationId: WorkflowSpecificationId,
                WorkflowInstanceId: WorkflowInstanceId,
                TaskSpecificationId: task.TaskSpecificationId,
                TaskInstanceId: task.TaskInstanceId,
                Timestamp: task.Status.Timestamp ?? DateTime.UtcNow));
        }
        catch
        {
        }
    }

    private void NotifyDependencyAdded(Task prerequisiteTask, Task dependentTask)
    {
        try
        {
            _observer.OnDependencyAdded(new DependencyAddedEvent(
                WorkflowSpecificationId: WorkflowSpecificationId,
                WorkflowInstanceId: WorkflowInstanceId,
                PrerequisiteTaskInstanceId: prerequisiteTask.TaskInstanceId,
                DependentTaskInstanceId: dependentTask.TaskInstanceId,
                Timestamp: DateTime.UtcNow));
        }
        catch
        {
        }
    }

}
