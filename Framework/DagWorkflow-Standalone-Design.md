# DagWorkflow Standalone Project Design

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
8. Make fan-out and fan-in first-class concepts instead of forcing callers to hand-build edge lists for common cases.
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
- `TaskFanInSpecification`

These types are inputs to the runtime. They are serializable, validation-friendly, and intended to be stable contracts.

### Runtime Model

The runtime model defines what is happening now.

- `Workflow`
- `Task`
- `WorkflowExecutionState`
- `TaskExecutionState`
- `WorkflowStatus`
- `TaskStatus`

These types own transitions, validation of state changes, and status snapshots.

### Execution Boundary

The execution boundary defines how task code is invoked.

- `ITaskExecutor`
- `IExecutionContext`
- `TaskExecutionResult`
- `WorkflowOrchestrator`

This is the seam between orchestration and user-defined work.

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

- `ExecutionOutput Output`
- `ErrorInfo Error`

This is preferred over storing raw `Exception` instances on status records.

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
- `TaskFanInSpecification`

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

### 3. Orchestration Layer

Owns scheduling and runtime mutation.

Primary types:

- `WorkflowOrchestrator`
- `TaskGraph`

Responsibilities:

- find ready tasks
- admit ready tasks to a queue
- enforce `MaxConcurrency`
- execute tasks through `ITaskExecutor`
- apply `TaskExecutionResult`
- apply runtime mutations such as spawned tasks and fan-in expansion

### 4. Extension Layer

Owns user-defined work.

Primary types:

- `ITaskExecutor`
- `IExecutionContext`

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

## Proposed Public API Shape

The standalone project should expose a compact public surface.

### Specifications

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
    string InputJson = null,
    TaskId? SpawnedByTaskId = null);

public readonly record struct TaskDependencySpecification(
    TaskId PrerequisiteTaskId,
    TaskId DependentTaskId);

public record TaskFanInSpecification(
    TaskId JoinTaskId,
    IReadOnlyCollection<TaskId> AdditionalPrerequisiteTaskIds = null,
    bool IncludeSpawnedTasks = true);
```

### Execution Boundary

```csharp
public interface IExecutionContext
{
    WorkflowId WorkflowId { get; }
    TaskId TaskId { get; }
    TaskSpecification TaskSpecification { get; }
    IReadOnlyDictionary<TaskId, TaskStatus> DependencyStatuses { get; }
    IReadOnlyDictionary<TaskId, ExecutionOutput?> DependencyOutputs { get; }
}

public interface ITaskExecutor
{
    TaskExecutionResult Execute(IExecutionContext executionContext);
}

public record TaskExecutionResult(
    ExecutionOutcome ExecutionOutcome,
    ExecutionFailureKind FailureKind = ExecutionFailureKind.None,
    ExecutionOutput Output = null,
    ErrorInfo Error = null,
    ExecutionRecoverability? Recoverability = null,
    IReadOnlyCollection<TaskSpecification> SpawnedTasks = null,
    IReadOnlyCollection<TaskDependencySpecification> AddedDependencies = null,
    IReadOnlyCollection<TaskFanInSpecification> FanInSpecifications = null);
```

### Runtime Entry Point

```csharp
Workflow workflow = Workflow.FromSpecification(specification);
workflow.RunToCompletion(taskExecutor);
WorkflowStatus status = workflow.Status;
```

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
    WorkflowId: new WorkflowId("StaticWorkflow"),
    Tasks: new[]
    {
        new TaskSpecification(new TaskId("A"), new TaskType("TaskA")),
        new TaskSpecification(new TaskId("B"), new TaskType("TaskB")),
        new TaskSpecification(new TaskId("C"), new TaskType("TaskC")),
        new TaskSpecification(new TaskId("D"), new TaskType("TaskD")),
        new TaskSpecification(new TaskId("E"), new TaskType("TaskE")),
        new TaskSpecification(new TaskId("F"), new TaskType("TaskF"))
    },
    Dependencies: new[]
    {
        new TaskDependencySpecification("A", "B"),
        new TaskDependencySpecification("A", "C"),
        new TaskDependencySpecification("B", "D"),
        new TaskDependencySpecification("C", "E"),
        new TaskDependencySpecification("D", "F"),
        new TaskDependencySpecification("E", "F")
    },
    MaxConcurrency: 2);
```

Execution behavior:

1. Build workflow from specification.
2. Validate structure before execution.
3. Admit ready tasks into the queue.
4. Execute tasks through `ITaskExecutor`.
5. Transition tasks and workflow to terminal state.

### Use Case 2: Dynamic Workflow With Runtime Fan-Out and Fan-In

The user-provided dynamic example is:

- `A`: scan a directory for MP4 files
- `B*`: one generated task per MP4 file
- `C`: aggregate results after all generated file tasks complete

Representative semantics:

1. `A` starts with a directory path as input.
2. `A` discovers N files.
3. `A` returns N new `TaskSpecification` instances for file-level work.
4. `A` returns a `TaskFanInSpecification` that wires all spawned tasks into `C`.
5. The orchestrator materializes those tasks and edges.
6. `C` becomes ready only after every generated file task finishes.

Initial static seed specification:

```csharp
WorkflowSpecification specification = new WorkflowSpecification(
    WorkflowId: new WorkflowId("Mp4Workflow"),
    Tasks: new[]
    {
        new TaskSpecification(
            TaskId: new TaskId("A"),
            TaskType: new TaskType("ScanMp4Directory"),
            InputType: new InputType("application/json"),
            InputJson: "{ \"path\": \"c:/media\" }"),
        new TaskSpecification(
            TaskId: new TaskId("C"),
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
        new TaskSpecification(new TaskId("B-1"), new TaskType("ProcessMp4"), new InputType("application/json"), "{ \"file\": \"a.mp4\" }"),
        new TaskSpecification(new TaskId("B-2"), new TaskType("ProcessMp4"), new InputType("application/json"), "{ \"file\": \"b.mp4\" }"),
        new TaskSpecification(new TaskId("B-3"), new TaskType("ProcessMp4"), new InputType("application/json"), "{ \"file\": \"c.mp4\" }")
    },
    fanInSpecifications: new[]
    {
        new TaskFanInSpecification(JoinTaskId: new TaskId("C"))
    });
```

Execution behavior:

1. `A` runs first.
2. `A` returns dynamically spawned `B-*` tasks.
3. `A` returns a fan-in declaration targeting `C`.
4. The orchestrator adds all `B-*` tasks at runtime.
5. The orchestrator expands the fan-in declaration into dependencies:
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
6. Apply any runtime graph mutations.
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
7. Runtime graph mutations are only allowed on successful task completion.

## Fan-Out and Fan-In Model

### Fan-Out

A task may return `SpawnedTasks` in `TaskExecutionResult`.

Each spawned task is:

- validated as a normal `TaskSpecification`
- added to the live workflow graph
- tagged with `SpawnedByTaskId` when omitted

### Fan-In

A task may return one or more `TaskFanInSpecification` values.

The orchestrator expands each fan-in declaration into concrete dependencies against:

- the spawned tasks from that same result, when `IncludeSpawnedTasks = true`
- any explicit `AdditionalPrerequisiteTaskIds`

This keeps the common join pattern compact and expressive.

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
    WorkflowId.cs
    TaskId.cs
    WorkflowSpecification.cs
    TaskSpecification.cs
    TaskDependencySpecification.cs
    TaskFanInSpecification.cs
    IExecutionContext.cs
    ITaskExecutor.cs
    TaskExecutionResult.cs
    ExecutionOutput.cs
    ErrorInfo.cs
    ExecutionPhase.cs
    ExecutionOutcome.cs
    ExecutionFailureKind.cs
    ExecutionRecoverability.cs
    TaskStatus.cs
    WorkflowStatus.cs

  DagWorkflow.Core/
    Workflow.cs
    Task.cs
    WorkflowExecutionState.cs
    TaskExecutionState.cs
    TaskGraph.cs
    WorkflowOrchestrator.cs
    ExecutionContext.cs
    ExecutionRecoverabilityDefaults.cs

  DagWorkflow.Defaults/
    DefaultTaskExecutor.cs

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

### Static Execution

- fixed DAG executes in dependency-safe order
- final workflow status is succeeded when all tasks succeed

### Dynamic Execution

- spawned tasks are added to the live graph
- spawned tasks inherit `SpawnedByTaskId` when omitted
- dynamic dependency additions are validated
- fan-in expands into correct concrete edges
- join task remains blocked until all fan-in prerequisites finish

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
4. Should fan-in be limited to spawned children from the current task result, or expanded into richer grouping semantics later?
5. Should task-level retry policy live in `TaskSpecification`, workflow defaults, or both?

## Recommended Next Milestone

The next standalone-project milestone should be:

1. extract DagWorkflow into its own solution
2. preserve the current public specification and status model
3. add tests for the two user-provided examples
4. keep the synchronous orchestrator for the first extraction
5. then add async worker-backed execution behind the same contracts

That sequence keeps the current design coherent while moving it toward a reusable standalone library.