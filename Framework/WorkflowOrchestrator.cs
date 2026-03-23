namespace OrganizeMedia.Framework;

public class WorkflowOrchestrator
{
    private readonly ITaskExecutor _taskExecutor;

    public WorkflowOrchestrator(ITaskExecutor taskExecutor = null)
    {
        _taskExecutor = taskExecutor ?? new DefaultTaskExecutor();
    }

    public void RunToCompletion(Workflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        Task currentTask = null;
        int maxConcurrency = workflow.MaxConcurrency ?? int.MaxValue;

        workflow.MarkReadyToRun();
        workflow.MarkQueued();
        workflow.MarkRunning();

        try
        {
            Queue<Task> readyQueue = new Queue<Task>();

            while (true)
            {
                EnqueueReadyTasks(workflow, readyQueue, maxConcurrency);

                if (readyQueue.Count == 0)
                {
                    if (workflow.GetTasks().All(IsTaskCompleteForWorkflowSuccess))
                    {
                        workflow.MarkSucceeded();
                        return;
                    }

                    throw new InvalidOperationException(
                        $"Workflow {workflow.WorkflowId} has unfinished tasks but no ready tasks. The graph may contain a cycle or a blocked dependency.");
                }

                currentTask = readyQueue.Dequeue();

                if (currentTask.Status.ExecutionPhase != ExecutionPhase.Queued)
                {
                    currentTask = null;
                    continue;
                }

                currentTask.MarkRunning();

                IExecutionContext executionContext = new ExecutionContext(workflow, currentTask);
                TaskExecutionResult executionResult = _taskExecutor.Execute(executionContext);
                ApplyExecutionResult(currentTask, executionResult);

                if (executionResult.ExecutionOutcome == ExecutionOutcome.Canceled)
                {
                    workflow.MarkCanceled(
                        output: executionResult.Output,
                        error: executionResult.Error,
                        recoverability: executionResult.Recoverability ?? ExecutionRecoverability.Retryable);
                    return;
                }

                if (executionResult.ExecutionOutcome == ExecutionOutcome.Failed)
                {
                    workflow.MarkFailed(
                        failureKind: executionResult.FailureKind,
                        output: executionResult.Output,
                        error: executionResult.Error,
                        recoverability: executionResult.Recoverability);
                    return;
                }

                workflow.ApplyRuntimeMutations(currentTask, executionResult);

                currentTask = null;
            }
        }
        catch (Exception exception)
        {
            if (currentTask is not null && currentTask.Status.ExecutionPhase == ExecutionPhase.Running)
            {
                currentTask.MarkFailed(exception, ExecutionFailureKind.Unknown);
            }

            if (workflow.Status.ExecutionPhase == ExecutionPhase.Running)
            {
                workflow.MarkFailed(exception, ExecutionFailureKind.Unknown);
            }

            throw;
        }
    }

    private static void EnqueueReadyTasks(Workflow workflow, Queue<Task> readyQueue, int maxConcurrency)
    {
        if (readyQueue.Count >= maxConcurrency)
            return;

        foreach (Task task in workflow.GetTasks())
        {
            if (readyQueue.Count >= maxConcurrency)
                return;

            if (!IsTaskSchedulable(task))
                continue;

            if (workflow.GetDependencies(task).All(IsDependencySatisfied))
            {
                MarkTaskReady(task, readyQueue);
            }
        }
    }

    private static bool IsTaskCompleteForWorkflowSuccess(Task task)
    {
        return task.Status.ExecutionPhase == ExecutionPhase.Finished &&
               !IsRestartRecoverability(task.Status.Recoverability);
    }

    private static bool IsTaskSchedulable(Task task)
    {
        return task.Status.ExecutionPhase == ExecutionPhase.NotStarted ||
               (task.Status.ExecutionPhase == ExecutionPhase.Finished &&
                IsRestartRecoverability(task.Status.Recoverability));
    }

    private static bool IsDependencySatisfied(Task dependency)
    {
        return dependency.Status.ExecutionPhase == ExecutionPhase.Finished &&
               !IsRestartRecoverability(dependency.Status.Recoverability);
    }

    private static bool IsRestartRecoverability(ExecutionRecoverability recoverability)
    {
        return recoverability == ExecutionRecoverability.Retryable ||
               recoverability == ExecutionRecoverability.Resumable;
    }

    private static void MarkTaskReady(Task task, Queue<Task> readyQueue)
    {
        task.MarkReadyToRun();
        task.MarkQueued();
        readyQueue.Enqueue(task);
    }

    private static void ApplyExecutionResult(Task task, TaskExecutionResult executionResult)
    {
        switch (executionResult.ExecutionOutcome)
        {
            case ExecutionOutcome.Succeeded:
                task.MarkSucceeded(executionResult.Output);
                break;

            case ExecutionOutcome.Canceled:
                task.MarkCanceled(
                    output: executionResult.Output,
                    error: executionResult.Error,
                    recoverability: executionResult.Recoverability ?? ExecutionRecoverability.Retryable);
                break;

            case ExecutionOutcome.Failed:
                task.MarkFailed(
                    failureKind: executionResult.FailureKind,
                    output: executionResult.Output,
                    error: executionResult.Error,
                    recoverability: executionResult.Recoverability);
                break;

            default:
                throw new InvalidOperationException(
                    $"Task executor returned unsupported outcome {executionResult.ExecutionOutcome} for task {task.TaskId}.");
        }
    }
}