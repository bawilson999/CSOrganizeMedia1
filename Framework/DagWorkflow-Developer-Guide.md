# DagWorkflow Developer Guide

## Purpose

This guide explains how to use the current DagWorkflow implementation as a consumer.

It focuses on the public API that exists today:

- defining workflows with specifications
- running workflows with the built-in executor or a custom executor
- reading workflow and task status
- observing transitions and runtime graph changes
- using dynamic task spawning and runtime dependency addition

This is a developer guide, not an internal architecture document. It is written against the implemented code in this repository.

## What DagWorkflow Provides

DagWorkflow is a synchronous DAG workflow engine with these core capabilities:

1. Static DAG execution.
2. Dynamic task creation at runtime.
3. Runtime dependency addition.
4. Rich task and workflow status snapshots.
5. Observer-based transition and graph-change reporting.
6. A small executor interface that lets you plug in your own task behavior.

The public entry point is simple:

```csharp
Workflow workflow = Workflow.FromSpecification(specification);
workflow.RunToCompletion(taskExecutor, observer);
WorkflowStatus status = workflow.Status;
```

## Public Types You Will Use Most Often

### Value Objects

DagWorkflow uses small value objects instead of raw strings for identifiers and task metadata.

```csharp
WorkflowId workflowId = new WorkflowId("W0");
TaskId taskId = new TaskId("A");
TaskType taskType = new TaskType("ScanMp4Directory");
InputType inputType = new InputType("application/json");
```

Current conventions:

- `WorkflowId` identifies one workflow instance.
- `TaskId` identifies one task within a workflow.
- `TaskType` is an open-ended label describing what a task does.
- `InputType` is an open-ended label describing the shape or media type of `InputJson`.

The framework does not treat `TaskType` or `InputType` as enums. They are labels chosen by the consumer.

### Specifications

These types define what should run.

```csharp
public record WorkflowSpecification(
    WorkflowId WorkflowId,
    IReadOnlyCollection<TaskSpecification> Tasks,
    IReadOnlyCollection<TaskDependencySpecification> Dependencies,
    int? MaxConcurrency = null);

public record TaskSpecification(
    TaskId TaskId,
    TaskType TaskType,
    InputType? InputType = null,
    string? InputJson = null,
    TaskId? SpawnedByTaskId = null);

public readonly record struct TaskDependencySpecification(
    TaskId PrerequisiteTaskId,
    TaskId DependentTaskId);
```

Important current validation rules:

- workflow ids must be non-empty
- task ids must be unique within a workflow
- dependency endpoints must exist
- self-dependencies are invalid
- duplicate dependencies are invalid
- dependency cycles are rejected
- `MaxConcurrency`, when provided, must be positive
- `TaskSpecification.InputJson` requires `InputType`

## Quick Start

### Smallest Working Example

This example uses the built-in `DefaultTaskExecutor`, which simply formats the task type and optional input into a `TextExecutionOutput`.

```csharp
using OrganizeMedia.Framework;

WorkflowSpecification specification = new WorkflowSpecification(
    WorkflowId: new WorkflowId("HelloWorkflow"),
    Tasks:
    [
        new TaskSpecification(
            TaskId: new TaskId("A"),
            TaskType: new TaskType("SayHello"),
            InputType: new InputType("application/json"),
            InputJson: "{ \"name\": \"world\" }")
    ],
    Dependencies: Array.Empty<TaskDependencySpecification>());

Workflow workflow = Workflow.FromSpecification(specification);
workflow.RunToCompletion();

WorkflowStatus status = workflow.Status;
TextExecutionOutput output = (TextExecutionOutput)status.TaskStatuses[new TaskId("A")].Output!;
Console.WriteLine(output.Value);
```

With the default executor, the output value for task `A` would be:

```text
SayHello(application/json): { "name": "world" }
```

## Running A Static Workflow

Static workflows define all tasks and dependencies before execution starts.

```csharp
using OrganizeMedia.Framework;

WorkflowSpecification specification = new WorkflowSpecification(
    WorkflowId: new WorkflowId("StaticWorkflow"),
    Tasks:
    [
        new TaskSpecification(new TaskId("A"), new TaskType("TaskA")),
        new TaskSpecification(new TaskId("B"), new TaskType("TaskB")),
        new TaskSpecification(new TaskId("C"), new TaskType("TaskC")),
        new TaskSpecification(new TaskId("D"), new TaskType("TaskD")),
        new TaskSpecification(new TaskId("E"), new TaskType("TaskE")),
        new TaskSpecification(new TaskId("F"), new TaskType("TaskF"))
    ],
    Dependencies:
    [
        new TaskDependencySpecification(new TaskId("A"), new TaskId("B")),
        new TaskDependencySpecification(new TaskId("A"), new TaskId("C")),
        new TaskDependencySpecification(new TaskId("B"), new TaskId("D")),
        new TaskDependencySpecification(new TaskId("C"), new TaskId("E")),
        new TaskDependencySpecification(new TaskId("D"), new TaskId("F")),
        new TaskDependencySpecification(new TaskId("E"), new TaskId("F"))
    ],
    MaxConcurrency: 2);

Workflow workflow = Workflow.FromSpecification(specification);
workflow.RunToCompletion();

WorkflowStatus finalStatus = workflow.Status;
```

What happens internally:

1. The specification is validated.
2. A runtime workflow is created from the specification.
3. Tasks whose dependencies are satisfied are admitted to the ready queue.
4. Tasks move through `ReadyToRun`, `Queued`, `Running`, and `Finished`.
5. The workflow reaches a terminal state when all tasks have completed successfully, or when a task returns a canceled or failed result.

## Using A Custom Executor

Real workflows typically provide their own `ITaskExecutor` implementation.

The executor receives an `IExecutionContext` and returns a `TaskExecutionResult`.

```csharp
using OrganizeMedia.Framework;

public sealed class SampleTaskExecutor : ITaskExecutor
{
    public TaskExecutionResult Execute(IExecutionContext executionContext)
    {
        TaskSpecification specification = executionContext.TaskSpecification;

        return specification.TaskType.Value switch
        {
            "ParseJson" => ExecuteParseJson(specification),
            "AggregateResults" => ExecuteAggregateResults(executionContext),
            _ => TaskExecutionResult.Failed(
                failureKind: ExecutionFailureKind.Unknown,
                error: new ErrorInfo(
                    Type: "UnknownTaskType",
                    Message: $"Unsupported task type {specification.TaskType}."))
        };
    }

    private static TaskExecutionResult ExecuteParseJson(TaskSpecification specification)
    {
        return TaskExecutionResult.Succeeded(
            new TextExecutionOutput($"parsed: {specification.InputJson}"));
    }

    private static TaskExecutionResult ExecuteAggregateResults(IExecutionContext executionContext)
    {
        string joined = string.Join(
            ",",
            executionContext.DependencyOutputs
                .OrderBy(pair => pair.Key.Value)
                .Select(pair => ((TextExecutionOutput)pair.Value!).Value));

        return TaskExecutionResult.Succeeded(
            new TextExecutionOutput($"aggregate: {joined}"));
    }
}
```

### What The Executor Can See

`IExecutionContext` exposes:

```csharp
public interface IExecutionContext
{
    WorkflowId WorkflowId { get; }
    TaskId TaskId { get; }
    TaskSpecification TaskSpecification { get; }
    IReadOnlyDictionary<TaskId, TaskStatus> DependencyStatuses { get; }
    IReadOnlyDictionary<TaskId, ExecutionOutput?> DependencyOutputs { get; }
}
```

Important details:

- `TaskSpecification` is the declarative definition for the current task.
- `DependencyStatuses` shows the current status snapshot for each prerequisite task.
- `DependencyOutputs` is keyed by dependency task id.
- dependency outputs are nullable by type, so task code should either handle missing outputs explicitly or assert that a specific dependency output is present.

### What The Executor Cannot Do

The executor does not get direct access to engine-owned mutable state.

It cannot:

- mutate task phase directly
- add tasks directly to the graph
- add dependencies directly to the graph
- mark the workflow succeeded, canceled, or failed directly

Instead, it communicates through `TaskExecutionResult`.

## Returning Results

The current API uses factory methods rather than a public positional constructor.

### Success

```csharp
return TaskExecutionResult.Succeeded(
    output: new TextExecutionOutput("done"));
```

### Cancel

```csharp
return TaskExecutionResult.Canceled(
    output: new TextExecutionOutput("stopped by policy"),
    error: new ErrorInfo(
        Type: "Cancellation",
        Message: "Execution was canceled."),
    recoverability: ExecutionRecoverability.Retryable);
```

### Failure

```csharp
return TaskExecutionResult.Failed(
    failureKind: ExecutionFailureKind.Transient,
    error: new ErrorInfo(
        Type: "NetworkError",
        Message: "The remote service timed out."));
```

### Result Rules Enforced By The Framework

The framework validates `TaskExecutionResult` shape.

Current enforced rules include:

- succeeded results cannot carry a failure kind
- succeeded results cannot carry explicit recoverability
- failed results must use `Transient`, `Permanent`, or `Unknown`
- canceled results cannot carry a failure kind
- graph changes are only allowed on succeeded results
- canceled and failed results must use terminal recoverability values when one is specified

## Observing Execution

DagWorkflow uses observers for transition and graph-change reporting.

### Built-In Text Observer

```csharp
using OrganizeMedia.Framework;

Workflow workflow = Workflow.FromSpecification(specification);
workflow.RunToCompletion(
    taskExecutor: new SampleTaskExecutor(),
    observer: new TextWriterWorkflowObserver(Console.Out));
```

The built-in text observer writes lines like these:

```text
/W0 ReadyToRun, Pending, None, AwaitingOutcome
/W0/A Running, Pending, None, AwaitingOutcome
/W0/B added by runtime graph change
/W0 dependency added: A -> B
```

### Custom Observer

You can implement `IWorkflowObserver` to collect telemetry, write logs, update a UI, or build test assertions.

```csharp
using OrganizeMedia.Framework;

public sealed class RecordingObserver : IWorkflowObserver
{
    public List<TaskTransitionEvent> TaskTransitions { get; } = new();
    public List<WorkflowTransitionEvent> WorkflowTransitions { get; } = new();

    public void OnTaskTransition(TaskTransitionEvent transitionEvent)
    {
        TaskTransitions.Add(transitionEvent);
    }

    public void OnWorkflowTransition(WorkflowTransitionEvent transitionEvent)
    {
        WorkflowTransitions.Add(transitionEvent);
    }

    public void OnTaskAdded(TaskAddedEvent taskAddedEvent)
    {
    }

    public void OnDependencyAdded(DependencyAddedEvent dependencyAddedEvent)
    {
    }
}
```

## Reading Status

`Workflow.Status` returns a snapshot of the current workflow state.

```csharp
WorkflowStatus workflowStatus = workflow.Status;
Console.WriteLine(workflowStatus.ExecutionPhase);
Console.WriteLine(workflowStatus.ExecutionOutcome);
Console.WriteLine(workflowStatus.Recoverability);
```

Task status is available through `WorkflowStatus.TaskStatuses`.

```csharp
TaskStatus taskStatus = workflow.Status.TaskStatuses[new TaskId("A")];

Console.WriteLine(taskStatus.ExecutionPhase);
Console.WriteLine(taskStatus.ExecutionOutcome);
Console.WriteLine(taskStatus.FailureKind);
Console.WriteLine(taskStatus.Recoverability);
Console.WriteLine(taskStatus.Timestamp);
```

### Status Axes

DagWorkflow models status with four distinct axes.

#### ExecutionPhase

- `NotStarted`
- `ReadyToRun`
- `Queued`
- `Running`
- `Finished`

#### ExecutionOutcome

- `Pending`
- `Succeeded`
- `Canceled`
- `Failed`

#### ExecutionFailureKind

- `None`
- `Transient`
- `Permanent`
- `Unknown`

#### ExecutionRecoverability

- `AwaitingOutcome`
- `NoRecoveryNeeded`
- `Retryable`
- `Resumable`
- `RequiresIntervention`
- `NotRecoverable`
- `Unknown`

Default recoverability is derived by `ExecutionRecoverabilityDefaults.From(...)`.

Current baseline mappings are:

- non-finished execution -> `AwaitingOutcome`
- succeeded -> `NoRecoveryNeeded`
- canceled -> `Retryable`
- failed + transient -> `Retryable`
- failed + permanent -> `NotRecoverable`
- failed + unknown or none -> `RequiresIntervention`

## Dynamic Task Spawning And Runtime Dependencies

DagWorkflow supports runtime task spawning and runtime dependency addition.

### When To Use It

Use dynamic task spawning when one task discovers additional work only at execution time.

Typical example:

1. Task `A` scans a directory.
2. Task `A` discovers files.
3. Task `A` returns one spawned task per file.
4. Task `C` should run only after all discovered file tasks finish.

### Returning Spawned Tasks

```csharp
return TaskExecutionResult.Succeeded(
    output: new TextExecutionOutput("Discovered 3 files"),
    spawnedTasks:
    [
        new TaskSpecification(
            TaskId: new TaskId("B-1"),
            TaskType: new TaskType("ProcessFile"),
            InputType: new InputType("application/json"),
            InputJson: "{ \"file\": \"a.mp4\" }"),
        new TaskSpecification(
            TaskId: new TaskId("B-2"),
            TaskType: new TaskType("ProcessFile"),
            InputType: new InputType("application/json"),
            InputJson: "{ \"file\": \"b.mp4\" }")
    ]);
```

If `SpawnedByTaskId` is omitted, the workflow runtime fills it in with the current task id.

### Returning Runtime Dependencies

```csharp
TaskSpecification[] spawnedTasks =
[
    new TaskSpecification(
        TaskId: new TaskId("B-1"),
        TaskType: new TaskType("ProcessFile"),
        InputType: new InputType("application/json"),
        InputJson: "{ \"file\": \"a.mp4\" }"),
    new TaskSpecification(
        TaskId: new TaskId("B-2"),
        TaskType: new TaskType("ProcessFile"),
        InputType: new InputType("application/json"),
        InputJson: "{ \"file\": \"b.mp4\" }")
];

return TaskExecutionResult.Succeeded(
    spawnedTasks: spawnedTasks,
    addedDependencies:
    [
        new TaskDependencySpecification(new TaskId("B-1"), new TaskId("C")),
        new TaskDependencySpecification(new TaskId("B-2"), new TaskId("C"))
    ]);
```

If you need additional prerequisites, add them explicitly:

```csharp
new TaskDependencySpecification(new TaskId("AUX"), new TaskId("C"))
```

### Inspecting Graph Changes

`TaskExecutionResult.GraphChanges` is always present. Results without runtime graph changes use `TaskGraphChanges.None`.

```csharp
TaskExecutionResult result = executor.Execute(executionContext);

foreach (TaskSpecification spawnedTask in result.GraphChanges.SpawnedTasks)
{
    Console.WriteLine($"spawned: {spawnedTask.TaskId}");
}

foreach (TaskDependencySpecification dependency in result.GraphChanges.AddedDependencies)
{
    Console.WriteLine($"dependency: {dependency.PrerequisiteTaskId} -> {dependency.DependentTaskId}");
}
```

### Dynamic Execution Example

```csharp
WorkflowSpecification specification = new WorkflowSpecification(
    WorkflowId: new WorkflowId("Mp4Workflow"),
    Tasks:
    [
        new TaskSpecification(
            TaskId: new TaskId("A"),
            TaskType: new TaskType("ScanMp4Directory"),
            InputType: new InputType("application/json"),
            InputJson: "{ \"path\": \"c:/media\" }"),
        new TaskSpecification(
            TaskId: new TaskId("C"),
            TaskType: new TaskType("AggregateMp4Results"))
    ],
    Dependencies: Array.Empty<TaskDependencySpecification>(),
    MaxConcurrency: 4);

Workflow workflow = Workflow.FromSpecification(specification);
workflow.RunToCompletion(new Mp4TaskExecutor());
```

### Graph Change Rules

Current runtime rules include:

- spawned tasks are validated like any other `TaskSpecification`
- runtime dependencies must reference existing tasks
- runtime dependencies cannot duplicate existing dependencies
- runtime dependencies cannot create cycles
- runtime dependencies can only target tasks that have not started execution
- graph changes are only applied for succeeded task results

## DefaultTaskExecutor

`DefaultTaskExecutor` is a convenience implementation, not a task router.

Current behavior:

- validates the current `TaskSpecification`
- if no `InputJson` is present, outputs the task type
- if `InputJson` is present, outputs `TaskType(InputType): InputJson`

Example:

```csharp
DefaultTaskExecutor executor = new DefaultTaskExecutor();
```

This is useful for smoke tests, examples, and simple demonstrations. Real applications will usually implement `ITaskExecutor` themselves.

## Concurrency Semantics

`WorkflowSpecification.MaxConcurrency` is a workflow-level scheduling control.

In the current synchronous implementation it means:

- cap how many tasks are admitted into the queued set at once

It does not currently mean true parallel execution. The orchestrator is still synchronous and executes one running task at a time.

This matters when reading logs and transition sequences:

- you may see multiple tasks admitted to `Queued`
- but only one task is actively executed at a time in the current implementation

## Common Patterns

### Use TaskType As A Router Key

Many executors switch on `TaskSpecification.TaskType`.

```csharp
return executionContext.TaskSpecification.TaskType.Value switch
{
    "ScanMp4Directory" => RunScan(executionContext),
    "ProcessMp4" => RunProcess(executionContext),
    "AggregateMp4Results" => RunAggregate(executionContext),
    _ => TaskExecutionResult.Failed(
        ExecutionFailureKind.Unknown,
        error: new ErrorInfo("UnknownTaskType", "Task type was not recognized."))
};
```

### Treat InputJson As Opaque Input Owned By Your Executor

The framework does not parse `InputJson` for you.

That means your executor owns:

- choosing the input schema
- choosing the `InputType` label
- parsing `InputJson`
- validating the payload content

### Use Observers For Logging Instead Of Ad Hoc Graph Dumps

The framework now uses transition and graph-change observability instead of graph-display helpers. If you want console output, prefer:

```csharp
workflow.RunToCompletion(observer: new TextWriterWorkflowObserver(Console.Out));
```

## Common Pitfalls

### 1. Providing InputJson Without InputType

This is invalid.

```csharp
new TaskSpecification(
    TaskId: new TaskId("A"),
    TaskType: new TaskType("ScanDirectory"),
    InputJson: "{ \"path\": \"c:/media\" }")
```

If `InputJson` is present, provide `InputType` too.

### 2. Assuming DependencyOutputs Is Always Non-Null

The type is:

```csharp
IReadOnlyDictionary<TaskId, ExecutionOutput?>
```

If your executor requires an output from a dependency, validate or assert it explicitly.

### 3. Returning Graph Changes From Failed Or Canceled Results

This is rejected by `TaskExecutionResult` validation.

Graph changes belong only on succeeded results.

### 4. Treating MaxConcurrency As True Parallelism

In the current implementation, `MaxConcurrency` controls queue admission, not simultaneous multi-threaded execution.

### 5. Constructing Workflow Or Task Directly

Consumers should not construct runtime `Workflow` or `Task` objects directly. Build workflows from specifications:

```csharp
Workflow workflow = Workflow.FromSpecification(specification);
```

## Testing Guidance

This framework is straightforward to test because execution is synchronous and the executor contract is narrow.

Typical tests assert:

- final workflow outcome
- executed task order
- task outputs
- spawned task inheritance of `SpawnedByTaskId`
- observer event sequences
- runtime dependency behavior

A minimal execution-order assertion looks like this:

```csharp
workflow.RunToCompletion(taskExecutor);

Assert.Equal(ExecutionOutcome.Succeeded, workflow.Status.ExecutionOutcome);
Assert.Equal(["T0", "T3", "T4", "T2", "T1"], taskExecutor.ExecutedTaskIds);
```

## Summary

The intended way to use DagWorkflow today is:

1. Build a `WorkflowSpecification`.
2. Create a runtime workflow with `Workflow.FromSpecification(...)`.
3. Provide an `ITaskExecutor` when you need real task behavior.
4. Optionally provide an `IWorkflowObserver` for logging or telemetry.
5. Run with `Workflow.RunToCompletion(...)`.
6. Inspect `WorkflowStatus` and `TaskStatus` afterward.

For simple scenarios, the built-in default executor and text observer are enough to demonstrate the engine.

For real scenarios, the key extension point is `ITaskExecutor`, and the key observability hook is `IWorkflowObserver`.