# DagWorkflow Design

## Purpose

DagWorkflow should evolve from a design exercise embedded in `DesignQuestions` into a standalone workflow orchestration library with a clear public API, a stable runtime model, and explicit extension points for static and dynamic DAG execution.

This document describes the target design, the architectural boundaries, the current model that should be preserved, and the changes required to make DagWorkflow a maintainable standalone project.

## Problem Statement

DagWorkflow needs to support two classes of workflows:

1. Static workflows.
   - The full DAG is known before execution starts.
   - Example: a fixed graph `A -> {B, C} -> {D, E} -> F`.

2. Dynamic workflows.
   - Some tasks discover additional work at runtime.
   - Example: task `A` scans a directory for MP4 files, dynamically creates one task per file, and then a final task `C` waits for all generated tasks to finish before aggregating results.

The system also needs to preserve rich status semantics:

- explicit phase transitions
- explicit terminal outcome
- explicit failure classification
- recoverability for retry and future checkpoint/resume use cases
- serializable output and error payloads
- transition timestamps for observability

## Design Goals

1. Support both static and dynamic DAG execution with a single coherent model.
2. Keep runtime state and declarative workflow definition separate.
3. Preserve guarded domain transitions on tasks and workflows.
4. Make task execution pluggable through a narrow executor contract.
5. Allow runtime graph mutation without giving task code direct access to mutable engine state.
6. Keep workflow state serializable and persistence-friendly.
7. Allow bounded scheduling through a workflow-level concurrency setting.
8. Make runtime graph mutation practical without exposing mutable engine state directly to callers.
9. Make it practical to publish as a reusable package with tests and documentation.

## Non-Goals

1. This design does not require distributed execution in the first release.
2. This design does not require durable workflow persistence in the first release, although it should be compatible with later persistence.
3. This design does not require async execution in the first standalone cut, though the public abstractions should not block it.
4. This design does not attempt to be a cron scheduler, event bus, or full distributed systems platform.

## Core Concepts

### Declarative Model

The declarative model defines what should run.

- `WorkflowSpecification`
- `TaskSpecification`
- `TaskDependencySpecification`

These types are inputs to the runtime. They are serializable, validation-friendly, and intended to be stable contracts.

### Identity And Type Labels

DagWorkflow uses small value objects instead of plain strings for identity and task metadata labels.

- `WorkflowTemplateId`
- `WorkflowInstanceId`
- `TaskTemplateId`
- `TaskType`
- `InputType`

Current semantics:

- `WorkflowTemplateId` identifies one reusable workflow template.
- `WorkflowInstanceId` identifies one concrete runtime workflow instance.
- `TaskTemplateId` is the template-scoped task identifier value object.
- `TaskType` is an open-ended task-kind label, not a closed enum.
- `InputType` is an open-ended input schema or media-type label, not a closed enum.
- Conversion from `string` into these value objects is explicit.
- Conversion from the value object back to `string` is implicit.

### Runtime Model

The runtime model defines what is happening now.

- `Workflow`
- `Task`
- `WorkflowExecutionState`
- `TaskExecutionState`
- `WorkflowStatus`
- `TaskStatus`

These types own transitions, validation of state changes, and status snapshots.

`Workflow` and `Task` are public runtime types, but they are not constructed directly by consumers. The implemented design uses specification-driven construction through `Workflow.FromSpecification(...)`, with task and workflow constructors kept internal to the engine.

### Execution Boundary

The execution boundary defines how task code is invoked.

- `ITaskExecutor`
- `IExecutionContext`
- `TaskExecutionResult`

This is the seam between orchestration and user-defined work.

`WorkflowOrchestrator`, `TaskGraph`, `ExecutionContext`, and transition helpers are internal implementation details in the current design. They matter architecturally, but they are not part of the consumer-facing execution contract.

### Observability Boundary

The implemented design exposes workflow observability as a first-class public surface.

- `IWorkflowObserver`
- `TaskTransitionEvent`
- `WorkflowTransitionEvent`
- `TaskAddedEvent`
- `DependencyAddedEvent`
- `TaskStatusFormatter`
- `WorkflowStatusFormatter`
- `TextWriterWorkflowObserver`

This keeps execution pluggable while also making transition and runtime-mutation reporting explicit and testable.

## Status Model

DagWorkflow currently uses four orthogonal status axes.

### ExecutionPhase

Lifecycle phase of a task or workflow:

- `NotStarted`
- `ReadyToRun`
- `Queued`
- `Running`
- `Finished`

Intended semantics:

- `ReadyToRun` means eligible for scheduling.
- `Queued` means admitted to an executor queue.
- `Running` means dequeued and actively executing.

### ExecutionOutcome

Terminal or effective result:

- `Pending`
- `Succeeded`
- `Canceled`
- `Failed`

### ExecutionFailureKind

Classification for handled failures:

- `None`
- `Transient`
- `Permanent`
- `Unknown`

### ExecutionRecoverability

Operational guidance for retry or resume:

- `AwaitingOutcome`
- `NoRecoveryNeeded`
- `Retryable`
- `Resumable`
- `RequiresIntervention`
- `NotRecoverable`
- `Unknown`

Recoverability defaults are derived from phase, outcome, and failure kind.

## Output and Error Model

Task and workflow statuses carry serializable payloads:

- `ExecutionOutput? Output`
- `ErrorInfo? Error`

This is preferred over storing raw `Exception` instances on status records.

Additional implementation details in the current design:

- `ExecutionOutput` is an abstract serializable payload with `OutputType` and `ToJson()`.
- `TextExecutionOutput` is the built-in concrete output type used by the default executor and tests.
- `ErrorInfo.FromException(Exception? exception)` converts exceptions into serializable error payloads and returns `null` when no exception is present.

The standalone project should keep this design.

## Architecture Overview

The standalone architecture should be split into four layers.

### 1. Specification Layer

Defines immutable workflow input:

- workflow identity
- task definitions
- dependencies
- concurrency policy

Primary types:

- `WorkflowSpecification`
- `TaskSpecification`
- `TaskDependencySpecification`

### 2. Domain Runtime Layer

Owns lifecycle rules and immutable status snapshots.

Primary types:

- `Workflow`
- `Task`
- `WorkflowExecutionState`
- `TaskExecutionState`
- `WorkflowStatus`
- `TaskStatus`

Rules belong here, not in external builders.

The current implementation also centralizes shared phase and recoverability rules in internal support types:

- `ExecutionStateCore`
- `ExecutionTransitionSupport`

### 3. Orchestration Layer

Owns scheduling and runtime graph changes.

Primary types:

- `WorkflowOrchestrator`
- `TaskGraph`
- `ExecutionContext`

Responsibilities:

- find ready tasks
- admit ready tasks to a queue
- enforce `MaxConcurrency`
- execute tasks through `ITaskExecutor`
- apply `TaskExecutionResult`
- apply runtime graph changes such as spawned tasks and added dependencies

In the current implementation this layer is entirely internal. Consumers drive execution through `Workflow.RunToCompletion(...)` rather than by interacting with `WorkflowOrchestrator` directly.

### 4. Extension Layer

Owns user-defined work.

Primary types:

- `ITaskExecutor`
- `IExecutionContext`
- `IWorkflowObserver`

The standalone library should keep this layer narrow so custom task behavior can vary without destabilizing orchestration.

## Why IExecutionContext Is Separate From ExecutionState

`ExecutionState` is engine-owned mutable runtime state.

It answers:

- what phase is this task in
- what was its outcome
- what failure kind applies
- what timestamps and payloads are recorded

`IExecutionContext` is executor-facing operational input.

It answers:

- what task is being executed
- what dependency outputs are available
- what dependency statuses are visible
- what workflow and task identity apply to this execution attempt

This separation prevents task handlers from mutating internal engine state directly while still giving them the information they need to do real work.

The current implementation also derives `DependencyOutputs` from dependency statuses on demand rather than storing a second mutable output dictionary in the execution context.

## Observability Model

DagWorkflow currently treats transition and runtime graph change reporting as part of the supported public API.

### Observer Contract

Observers receive notifications for:

- task transitions
- workflow transitions
- runtime task additions
- runtime dependency additions

This is the mechanism now used for console and text output. The earlier graph-display-oriented console snapshot approach has been removed in favor of state-transition observability.

### Built-In Observer And Formatters

The framework currently ships with:

- `TextWriterWorkflowObserver`
- `TaskStatusFormatter`
- `WorkflowStatusFormatter`

`TextWriterWorkflowObserver` formats task and workflow transitions through the formatter types and writes graph-change events as plain text lines.

## Implemented Public API Shape

The current framework exposes a compact public surface, with orchestration kept internal and consumers entering through specifications, execution interfaces, statuses, observers, and convenience utilities.

### Value Objects

```csharp
public readonly record struct WorkflowTemplateId(string Value);

public readonly record struct WorkflowInstanceId(WorkflowTemplateId WorkflowTemplateId, int InstanceNumber);

public readonly record struct TaskTemplateId(string Value);

public readonly record struct TaskType(string Value);

public readonly record struct InputType(string Value);
```

### Specifications

```csharp
public record WorkflowSpecification(
    WorkflowTemplateId WorkflowTemplateId,
    IReadOnlyCollection<TaskSpecification> Tasks,
    IReadOnlyCollection<TaskDependencySpecification> Dependencies,
    int? MaxConcurrency = null);

public record TaskSpecification(
    TaskTemplateId TaskTemplateId,
    TaskType TaskType,
    InputType? InputType = null,
    string? InputJson = null,
    TaskCardinality Cardinality = TaskCardinality.Singleton);

public readonly record struct TaskDependencySpecification(
    TaskTemplateId PrerequisiteTaskTemplateId,
    TaskTemplateId DependentTaskTemplateId);
```

The implemented public surface uses `TaskTemplateId`, `PrerequisiteTaskTemplateId`, and `DependentTaskTemplateId` as the final template-identity names, distinct from runtime `TaskInstanceId` values.

### Execution Boundary

```csharp
public interface IExecutionContext
{
    WorkflowTemplateId WorkflowTemplateId { get; }
    WorkflowInstanceId WorkflowInstanceId { get; }
    TaskTemplateId TaskTemplateId { get; }
    TaskInstanceId TaskInstanceId { get; }
    TaskInstanceId? SpawnedByTaskInstanceId { get; }
    TaskSpecification TaskSpecification { get; }
    IReadOnlyDictionary<TaskInstanceId, TaskStatus> DependencyStatuses { get; }
    IReadOnlyDictionary<TaskInstanceId, ExecutionOutput?> DependencyOutputs { get; }
}


`TaskTemplateId` and `TaskInstanceId` intentionally model different concepts at this boundary: template identity vs. runtime instance identity.
public interface ITaskExecutor
{
    TaskExecutionResult Execute(IExecutionContext executionContext);
}

public sealed record TaskGraphChanges(
    IReadOnlyCollection<TaskSpecification> SpawnedTasks,
    IReadOnlyCollection<TaskDependencySpecification> AddedDependencies);

public sealed record TaskExecutionResult
{
    public ExecutionOutcome ExecutionOutcome { get; }
    public ExecutionFailureKind FailureKind { get; }
    public ExecutionOutput? Output { get; }
    public ErrorInfo? Error { get; }
    public ExecutionRecoverability? Recoverability { get; }
    public TaskGraphChanges GraphChanges { get; }

    public static TaskExecutionResult Succeeded(
        ExecutionOutput? output = null,
        IReadOnlyCollection<TaskSpecification>? spawnedTasks = null,
        IReadOnlyCollection<TaskDependencySpecification>? addedDependencies = null);

    public static TaskExecutionResult Canceled(
        ExecutionOutput? output = null,
        ErrorInfo? error = null,
        ExecutionRecoverability recoverability = ExecutionRecoverability.Retryable);

    public static TaskExecutionResult Failed(
        ExecutionFailureKind failureKind,
        ExecutionOutput? output = null,
        ErrorInfo? error = null,
        ExecutionRecoverability? recoverability = null);
}
```

Implementation notes for `TaskExecutionResult`:

- success results may carry graph changes
- canceled and failed results may not carry graph changes
- succeeded results may not carry a failure kind or explicit recoverability
- failed results must carry a non-`None` `ExecutionFailureKind`
- canceled and failed results must use terminal recoverability values when one is specified

### Observability

```csharp
public interface IWorkflowObserver
{
    void OnTaskTransition(TaskTransitionEvent transitionEvent);
    void OnWorkflowTransition(WorkflowTransitionEvent transitionEvent);
    void OnTaskAdded(TaskAddedEvent taskAddedEvent);
    void OnDependencyAdded(DependencyAddedEvent dependencyAddedEvent);
}

public static class TaskStatusFormatter
{
    public static string Format(WorkflowInstanceId workflowInstanceId, TaskInstanceId taskInstanceId, TaskStatus status);
}

public static class WorkflowStatusFormatter
{
    public static string Format(WorkflowInstanceId workflowInstanceId, WorkflowStatus status);
}

public sealed class TextWriterWorkflowObserver : IWorkflowObserver;
```

### Status And Output Types

```csharp
public abstract record ExecutionOutput
{
    public virtual string OutputType { get; }
    public string ToJson();
}

public record TextExecutionOutput(string Value) : ExecutionOutput;

public record ErrorInfo(
    string Type,
    string Message,
    string? StackTrace = null,
    string? Source = null,
    string? HelpLink = null,
    Dictionary<string, string?>? Data = null,
    ErrorInfo? InnerError = null);

public record TaskStatus(
    WorkflowTemplateId WorkflowTemplateId,
    WorkflowInstanceId WorkflowInstanceId,
    TaskTemplateId TaskTemplateId,
    TaskInstanceId TaskInstanceId,
    ExecutionPhase ExecutionPhase = ExecutionPhase.NotStarted,
    ExecutionOutcome ExecutionOutcome = ExecutionOutcome.Pending,
    ExecutionFailureKind FailureKind = ExecutionFailureKind.None,
    ExecutionRecoverability Recoverability = ExecutionRecoverability.AwaitingOutcome,
    int TotalSteps = 1,
    int CompletedSteps = 0,
    ErrorInfo? Error = null,
    ExecutionOutput? Output = null,
    DateTime? Timestamp = null,
    DateTime? CreatedTimestamp = null,
    DateTime? QueuedTimestamp = null,
    DateTime? ReadyToRunTimestamp = null,
    DateTime? RunningTimestamp = null,
    DateTime? FinishedTimestamp = null);

public record WorkflowStatus(
    WorkflowTemplateId WorkflowTemplateId,
    WorkflowInstanceId WorkflowInstanceId,
    ExecutionPhase ExecutionPhase,
    ExecutionOutcome ExecutionOutcome,
    ExecutionFailureKind FailureKind,
    ExecutionRecoverability Recoverability,
    Dictionary<TaskInstanceId, TaskStatus> TaskStatuses,
    ErrorInfo? Error = null,
    ExecutionOutput? Output = null,
    DateTime? Timestamp = null,
    DateTime? CreatedTimestamp = null,
    DateTime? QueuedTimestamp = null,
    DateTime? ReadyToRunTimestamp = null,
    DateTime? RunningTimestamp = null,
    DateTime? FinishedTimestamp = null);
```

### Runtime Entry Point

```csharp
Workflow workflow = Workflow.FromSpecification(specification);
workflow.RunToCompletion(
    taskExecutor,
    observer: new TextWriterWorkflowObserver(Console.Out));

WorkflowStatus status = workflow.Status;
```

Current runtime entry semantics:

- `Workflow.FromSpecification(...)` is the public construction path
- `Workflow.RunToCompletion(...)` accepts an optional executor and optional observer
- omitting the executor uses `DefaultTaskExecutor`
- omitting the observer uses the built-in null observer

## Example Use Cases

### Use Case 1: Static Workflow

The user-provided static example is a fixed graph:

- `A`
- `B`
- `C`
- `D`
- `E`
- `F`

One representative dependency layout is:

- `A -> B`
- `A -> C`
- `B -> D`
- `C -> E`
- `D -> F`
- `E -> F`

This is a standard static DAG. All tasks and all edges are known before execution begins.

The specification would look conceptually like this:

```csharp
WorkflowSpecification specification = new WorkflowSpecification(
    WorkflowTemplateId: new WorkflowTemplateId("StaticWorkflow"),
    Tasks: new[]
    {
        new TaskSpecification(new TaskTemplateId("A"), new TaskType("TaskA")),
        new TaskSpecification(new TaskTemplateId("B"), new TaskType("TaskB")),
        new TaskSpecification(new TaskTemplateId("C"), new TaskType("TaskC")),
        new TaskSpecification(new TaskTemplateId("D"), new TaskType("TaskD")),
        new TaskSpecification(new TaskTemplateId("E"), new TaskType("TaskE")),
        new TaskSpecification(new TaskTemplateId("F"), new TaskType("TaskF"))
    },
    Dependencies: new[]
    {
        new TaskDependencySpecification(new TaskTemplateId("A"), new TaskTemplateId("B")),
        new TaskDependencySpecification(new TaskTemplateId("A"), new TaskTemplateId("C")),
        new TaskDependencySpecification(new TaskTemplateId("B"), new TaskTemplateId("D")),
        new TaskDependencySpecification(new TaskTemplateId("C"), new TaskTemplateId("E")),
        new TaskDependencySpecification(new TaskTemplateId("D"), new TaskTemplateId("F")),
        new TaskDependencySpecification(new TaskTemplateId("E"), new TaskTemplateId("F"))
    },
    MaxConcurrency: 2);
```

Execution behavior:

1. Build workflow from specification.
2. Validate structure before execution.
3. Admit ready tasks into the queue.
4. Execute tasks through `ITaskExecutor`.
5. Transition tasks and workflow to terminal state.

### Use Case 2: Dynamic Workflow With Runtime Task Spawning and Join Dependencies

The user-provided dynamic example is:

- `A`: scan a directory for MP4 files
- `B*`: one generated task per MP4 file
- `C`: aggregate results after all generated file tasks complete

Representative semantics:

1. `A` starts with a directory path as input.
2. `A` discovers N files.
3. `A` returns N new `TaskSpecification` instances for file-level work.
4. `A` returns runtime-added dependencies that wire all spawned tasks into `C`.
5. The orchestrator materializes those tasks and edges.
6. `C` becomes ready only after every generated file task finishes.

Initial static seed specification:

```csharp
WorkflowSpecification specification = new WorkflowSpecification(
    WorkflowTemplateId: new WorkflowTemplateId("Mp4Workflow"),
    Tasks: new[]
    {
        new TaskSpecification(
            TaskTemplateId: new TaskTemplateId("A"),
            TaskType: new TaskType("ScanMp4Directory"),
            InputType: new InputType("application/json"),
            InputJson: "{ \"path\": \"c:/media\" }"),
        new TaskSpecification(
            TaskTemplateId: new TaskTemplateId("C"),
            TaskType: new TaskType("AggregateMp4Results"))
    },
    Dependencies: Array.Empty<TaskDependencySpecification>(),
    MaxConcurrency: 4);
```

Example result returned by task `A`:

```csharp
return TaskExecutionResult.Succeeded(
    output: new TextExecutionOutput("Discovered 3 mp4 files"),
    spawnedTasks: new[]
    {
        new TaskSpecification(new TaskTemplateId("B-1"), new TaskType("ProcessMp4"), new InputType("application/json"), "{ \"file\": \"a.mp4\" }"),
        new TaskSpecification(new TaskTemplateId("B-2"), new TaskType("ProcessMp4"), new InputType("application/json"), "{ \"file\": \"b.mp4\" }"),
        new TaskSpecification(new TaskTemplateId("B-3"), new TaskType("ProcessMp4"), new InputType("application/json"), "{ \"file\": \"c.mp4\" }")
    },
    addedDependencies: new[]
    {
        new TaskDependencySpecification(new TaskTemplateId("B-1"), new TaskTemplateId("C")),
        new TaskDependencySpecification(new TaskTemplateId("B-2"), new TaskTemplateId("C")),
        new TaskDependencySpecification(new TaskTemplateId("B-3"), new TaskTemplateId("C"))
    });
```

Execution behavior:

1. `A` runs first.
2. `A` returns dynamically spawned `B-*` tasks.
3. `A` returns explicit runtime dependencies targeting `C`.
4. The orchestrator adds all `B-*` tasks at runtime.
5. The orchestrator adds the returned dependencies:
   - `B-1 -> C`
   - `B-2 -> C`
   - `B-3 -> C`
6. `C` waits for all generated children.
7. `MaxConcurrency = 4` caps how many ready tasks are admitted at once.

This is the key design case that requires a live graph instead of a single upfront topological pass.

## Scheduling Model

The standalone project should keep the current scheduler semantics and refine them later for async execution.

### Current Intent

1. Build a live ready queue from tasks whose dependencies are all finished.
2. Move ready tasks through `ReadyToRun -> Queued`.
3. Dequeue one task at a time into `Running`.
4. Execute through `ITaskExecutor`.
5. Apply task result.
6. Apply any graph changes.
7. Re-scan the live graph for newly ready tasks.

### MaxConcurrency

`WorkflowSpecification.MaxConcurrency` should remain a workflow-level scheduling control.

In the current synchronous model it means:

- cap how many tasks are admitted into the queued set at once

In a future async or worker-backed model it should mean:

- cap how many tasks may be in `Running` simultaneously

The standalone design should preserve the property now and refine runtime behavior later.

## Graph Integrity Rules

The standalone project should preserve and strengthen runtime graph validation.

Rules:

1. Task ids must be unique within a workflow.
2. Dependencies may only reference existing tasks.
3. Self-dependencies are invalid.
4. Duplicate dependencies are invalid.
5. New runtime dependencies may not introduce cycles.
6. Runtime dependencies may only target tasks that have not started yet.
7. Graph changes are only allowed on successful task completion.

## Graph Change Model

### Task Spawning

A task may return `SpawnedTasks` in `TaskExecutionResult.Succeeded(...)` as a low-level escape hatch.

The preferred public model is to declare zero-to-many templates with `TaskCardinality.ZeroToMany` and materialize concrete runtime instances through `TaskTemplateSpawn`.

Each spawned task is:

- validated as a normal `TaskSpecification`
- added to the live workflow graph
- runtime task instances record their parent instance when spawned

Each template spawn is:

- resolved against an existing task template in the immutable specification graph
- normalized into a concrete singleton runtime task instance
- added to the live workflow graph with explicit runtime provenance

### Runtime-Added Dependencies

A task may return `AddedDependencies` in `TaskExecutionResult.Succeeded(...)`.

These are ordinary `TaskDependencySpecification` values applied to the live graph after the current task succeeds.

For instance-aware materialization, a task may also return `AddedInstanceDependencies`, which resolve dependencies against concrete runtime instances or spawn keys created within the same graph-change payload.

The current implementation groups runtime graph changes under `TaskGraphChanges`, which is the single public graph-change payload exposed on `TaskExecutionResult`.

All task results expose a non-null `GraphChanges` payload. Results without graph changes use `TaskGraphChanges.None`.

## Persistence Direction

The standalone project should be persistence-friendly even if persistence is not implemented immediately.

Recommended shape:

### Columns or structured fields

- workflow id
- task id
- execution phase
- execution outcome
- failure kind
- recoverability
- timestamps
- step counts

### JSON payloads

- `ExecutionOutput`
- `ErrorInfo`
- `TaskSpecification.InputJson`
- dynamic task spawn payloads

This allows snapshots and restart metadata without serializing runtime implementation details directly.

## Packaging Recommendation

DagWorkflow should move into its own solution with a structure like this:

```text
DagWorkflow.sln

src/
  DagWorkflow.Abstractions/
    WorkflowTemplateId.cs
    WorkflowInstanceId.cs
    TaskTemplateId.cs
    TaskType.cs
    InputType.cs
    WorkflowSpecification.cs
    TaskSpecification.cs
    TaskDependencySpecification.cs
    IExecutionContext.cs
    ITaskExecutor.cs
    IWorkflowObserver.cs
    TaskExecutionResult.cs
    TaskGraphChanges.cs
    ExecutionOutput.cs
    ErrorInfo.cs
    ExecutionPhase.cs
    ExecutionOutcome.cs
    ExecutionFailureKind.cs
    ExecutionRecoverability.cs
    TaskStatus.cs
    WorkflowStatus.cs
    TaskTransitionEvent.cs
    WorkflowTransitionEvent.cs
    TaskAddedEvent.cs
    DependencyAddedEvent.cs
    TaskStatusFormatter.cs
    WorkflowStatusFormatter.cs

  DagWorkflow.Core/
    Workflow.cs
    Task.cs
    WorkflowExecutionState.cs
    TaskExecutionState.cs
    TaskGraph.cs
    WorkflowOrchestrator.cs
    ExecutionContext.cs
    ExecutionStateCore.cs
    ExecutionTransitionSupport.cs
    ExecutionRecoverabilityDefaults.cs

  DagWorkflow.Defaults/
    DefaultTaskExecutor.cs
    TextWriterWorkflowObserver.cs

tests/
  DagWorkflow.Core.Tests/
  DagWorkflow.Integration.Tests/
```

Rationale:

- `Abstractions` stays small and stable for consumers.
- `Core` contains orchestration and guarded domain behavior.
- `Defaults` contains convenience implementations that consumers may replace.
- tests stay independent of the current interview-question solution.

## Test Plan For Standalone Project

The standalone project should include tests for the following behaviors.

### Specification Validation

- duplicate task ids rejected
- missing dependency target rejected
- self-dependency rejected
- cycle rejected
- invalid `MaxConcurrency` rejected

### Transition Rules

- invalid phase transitions rejected
- failed states require non-`None` `ExecutionFailureKind`
- recoverability defaults derived correctly
- non-success task results reject graph changes

### Static Execution

- fixed DAG executes in dependency-safe order
- final workflow status is succeeded when all tasks succeed

### Dynamic Execution

- spawned tasks are added to the live graph
- spawned task provenance is stored on runtime task instances, not on task templates
- dynamic dependency additions are validated
- join dependencies create the correct concrete edges
- join task remains blocked until all added prerequisites finish

### Observability

- task transitions are emitted through `IWorkflowObserver`
- workflow transitions are emitted through `IWorkflowObserver`
- graph-change events are emitted for task additions and dependency additions
- formatter output remains stable for task and workflow status lines

### Concurrency Controls

- `MaxConcurrency` limits queued admission in the synchronous scheduler
- later async implementation limits simultaneous running tasks

### Failure Semantics

- transient failure yields retryable status by default
- permanent failure yields not recoverable status by default
- unknown failure yields requires intervention by default

## Migration Plan

### Phase 1

Extract current DagWorkflow files into a dedicated solution.

### Phase 2

Split public abstractions from internal orchestration implementation.

### Phase 3

Add unit and integration tests around static and dynamic examples.

### Phase 4

Introduce async execution and worker-backed concurrency while preserving the same specification and status contracts.

### Phase 5

Add optional persistence and restart support.

## Open Design Questions

1. Should `ITaskExecutor` become async in the first standalone version, or should async be a second-step migration?
2. Should `TaskSpecification.InputJson` remain string-based, or should there be a stronger typed payload abstraction?
3. Should a durable persistence abstraction be part of v1, or layered later?
4. Should the framework eventually add higher-level join helpers, or keep only explicit runtime dependency addition?
5. Should task-level retry policy live in `TaskSpecification`, workflow defaults, or both?

## Recommended Next Milestone

The next standalone-project milestone should be:

1. extract DagWorkflow into its own solution
2. preserve the current public specification and status model
3. add tests for the two user-provided examples
4. keep the synchronous orchestrator for the first extraction
5. then add async worker-backed execution behind the same contracts

That sequence keeps the current design coherent while moving it toward a reusable standalone library.
