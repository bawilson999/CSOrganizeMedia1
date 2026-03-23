namespace OrganizeMedia.Framework;

public class Workflow
{
    private readonly TaskGraph _taskGraph;
    private readonly WorkflowExecutionState _executionState;
    private readonly Dictionary<TaskInstanceId, Task> _tasksByInstanceId;
    private readonly Dictionary<TaskTemplateId, List<Task>> _tasksByTemplateId;
    private readonly Dictionary<TaskTemplateId, int> _nextInstanceNumberByTaskTemplateId;
    private readonly Dictionary<TaskTemplateId, TaskSpecification> _taskTemplatesById;
    private IWorkflowObserver _observer = NullWorkflowObserver.Instance;

    internal Workflow(WorkflowTemplateId workflowTemplateId, int? maxConcurrency)
    {
        WorkflowTemplateId = workflowTemplateId;
        WorkflowInstanceId = new WorkflowInstanceId(workflowTemplateId, 1);
        MaxConcurrency = maxConcurrency;
        _taskGraph = new TaskGraph();
        _executionState = new WorkflowExecutionState(WorkflowInstanceId);
        _tasksByInstanceId = new Dictionary<TaskInstanceId, Task>();
        _tasksByTemplateId = new Dictionary<TaskTemplateId, List<Task>>();
        _nextInstanceNumberByTaskTemplateId = new Dictionary<TaskTemplateId, int>();
        _taskTemplatesById = new Dictionary<TaskTemplateId, TaskSpecification>();
    }

    public WorkflowTemplateId WorkflowTemplateId { get; init; }

    public WorkflowInstanceId WorkflowInstanceId { get; init; }

    public int? MaxConcurrency { get; }

    public static Workflow FromSpecification(WorkflowSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        specification.Validate();

        Workflow workflow = new Workflow(specification.WorkflowTemplateId, specification.MaxConcurrency);

        foreach (TaskSpecification taskSpecification in specification.Tasks)
        {
            workflow.RegisterTaskTemplate(taskSpecification);

            for (int instanceIndex = 0; instanceIndex < taskSpecification.InitialInstanceCount; instanceIndex++)
            {
                Task task = workflow.CreateTask(taskSpecification);
                workflow.AddTask(task);
            }
        }

        foreach (TaskDependencySpecification dependency in specification.Dependencies)
        {
            if (workflow.TryGetSingleTask(dependency.PrerequisiteTaskTemplateId, out Task? prerequisiteTask) &&
                workflow.TryGetSingleTask(dependency.DependentTaskTemplateId, out Task? dependentTask))
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
        if (!_tasksByTemplateId.TryGetValue(task.TaskTemplateId, out List<Task>? taskInstances))
        {
            taskInstances = new List<Task>();
            _tasksByTemplateId.Add(task.TaskTemplateId, taskInstances);
        }

        taskInstances.Add(task);
        _executionState.AddTaskExecutionState(task.ExecutionState);
    }

    private void RegisterTaskTemplate(TaskSpecification taskSpecification)
    {
        _taskTemplatesById.Add(taskSpecification.TaskTemplateId, taskSpecification);
    }

    private Task CreateTask(TaskSpecification taskSpecification, TaskInstanceId? spawnedByTaskInstanceId = null)
    {
        TaskTemplateId taskTemplateId = taskSpecification.TaskTemplateId;
        int nextInstanceNumber = _nextInstanceNumberByTaskTemplateId.TryGetValue(taskTemplateId, out int currentInstanceNumber)
            ? currentInstanceNumber + 1
            : 1;

        _nextInstanceNumberByTaskTemplateId[taskTemplateId] = nextInstanceNumber;
        return new Task(
            WorkflowInstanceId,
            taskSpecification,
            new TaskInstanceId(taskTemplateId, nextInstanceNumber),
            spawnedByTaskInstanceId);
    }

    private Task GetRequiredSingleTask(TaskTemplateId taskId)
    {
        if (!_tasksByTemplateId.TryGetValue(taskId, out List<Task>? taskInstances) || taskInstances.Count == 0)
        {
            throw new InvalidOperationException($"Workflow {WorkflowInstanceId} references missing task template {taskId}.");
        }

        if (taskInstances.Count != 1)
        {
            throw new InvalidOperationException(
                $"Workflow {WorkflowInstanceId} cannot resolve template task {taskId} to a single runtime instance because {taskInstances.Count} instances exist.");
        }

        return taskInstances[0];
    }

    private bool TryGetSingleTask(TaskTemplateId taskId, out Task? task)
    {
        task = null;

        if (!_tasksByTemplateId.TryGetValue(taskId, out List<Task>? taskInstances) || taskInstances.Count == 0)
        {
            return false;
        }

        if (taskInstances.Count != 1)
        {
            throw new InvalidOperationException(
                $"Workflow {WorkflowInstanceId} cannot resolve template task {taskId} to a single runtime instance because {taskInstances.Count} instances exist.");
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
        IReadOnlyCollection<TaskTemplateSpawn> spawnedTaskTemplates = graphChanges.SpawnedTaskTemplates;
        IReadOnlyCollection<TaskInstanceDependency> addedInstanceDependencies = graphChanges.AddedInstanceDependencies;
        Dictionary<string, Task> spawnedTasksByKey = new Dictionary<string, Task>(StringComparer.Ordinal);

        foreach (TaskSpecification spawnedTaskSpecification in spawnedTasks)
        {
            RuntimeAddTask(spawnedTaskSpecification, currentTask.TaskInstanceId);
        }

        foreach (TaskTemplateSpawn taskTemplateSpawn in spawnedTaskTemplates)
        {
            if (string.IsNullOrWhiteSpace(taskTemplateSpawn.SpawnKey))
            {
                throw new InvalidOperationException("Spawned task templates must provide a non-empty SpawnKey.");
            }

            Task spawnedTask = RuntimeSpawnTaskFromTemplate(currentTask, taskTemplateSpawn);

            if (!spawnedTasksByKey.TryAdd(taskTemplateSpawn.SpawnKey, spawnedTask))
            {
                throw new InvalidOperationException(
                    $"Workflow {WorkflowInstanceId} received duplicate spawned task key {taskTemplateSpawn.SpawnKey} in one graph change.");
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

    private Task RuntimeSpawnTaskFromTemplate(Task currentTask, TaskTemplateSpawn taskTemplateSpawn)
    {
        if (!_taskTemplatesById.TryGetValue(taskTemplateSpawn.TaskTemplateId, out TaskSpecification? taskTemplate))
        {
            throw new InvalidOperationException(
            $"Workflow {WorkflowInstanceId} references missing task template {taskTemplateSpawn.TaskTemplateId}.");
        }

        InputType? effectiveInputType = taskTemplateSpawn.InputType ?? taskTemplate.InputType;
        string? effectiveInputJson = taskTemplateSpawn.InputJson ?? taskTemplate.InputJson;

        TaskSpecification instanceSpecification = taskTemplate with
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
        Task prerequisiteTask = GetRequiredSingleTask(dependency.PrerequisiteTaskTemplateId);
        Task dependentTask = GetRequiredSingleTask(dependency.DependentTaskTemplateId);

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

        if (reference.TaskTemplateId is not null)
        {
            return GetRequiredSingleTask(reference.TaskTemplateId.Value);
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
                WorkflowTemplateId: WorkflowTemplateId,
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
                WorkflowTemplateId: WorkflowTemplateId,
                WorkflowInstanceId: WorkflowInstanceId,
                TaskTemplateId: task.TaskTemplateId,
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
                WorkflowTemplateId: WorkflowTemplateId,
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
