# DagWorkflow Functional Requirements

## Purpose

This document defines the functional requirements for DagWorkflow as implemented in this repository.

It answers a different question than the design and usage documents:

- the design document explains architectural shape and boundaries
- the usage guide explains how consumers use the API
- this document states what the system is required to do

The intent is to capture behavior in a form suitable for discussion, validation, regression review, and future extraction into a standalone package.

## Scope

This document covers the current synchronous DagWorkflow engine and its consumer-facing behavior, including:

- workflow specification and validation
- static workflow execution
- dynamic task spawning and runtime dependency addition
- task execution through a pluggable executor
- workflow and task status reporting
- recoverability and retry-oriented rerun behavior
- observer-based transition and mutation reporting
- built-in default execution behavior

This document does not define non-functional targets such as throughput, distributed execution, storage durability, or asynchronous worker coordination.

## Product Summary

DagWorkflow is a DAG workflow orchestration library.

The library allows consumers to:

1. Define a workflow as tasks and dependencies.
2. Create a runtime workflow from that definition.
3. Execute tasks through a user-supplied executor.
4. Inspect status snapshots for the workflow and its tasks.
5. Observe state transitions and runtime graph mutations.
6. Dynamically create additional tasks and dependencies at execution time.

## Actors

### Consumer

The consumer is application code that defines workflow specifications, supplies an executor, optionally supplies observers, runs workflows, and inspects results.

### Executor

The executor is user-provided code implementing `ITaskExecutor`. It performs task-specific work and returns a `TaskExecutionResult`.

### Observer

The observer is optional user-provided or built-in code implementing `IWorkflowObserver`. It receives workflow and task transition events and runtime mutation events.

### Workflow Engine

The workflow engine is the DagWorkflow runtime. It validates specifications, schedules tasks, applies result semantics, mutates the graph when permitted, and maintains workflow and task state.

## Assumptions And Current Constraints

1. The current engine is synchronous.
2. The current engine executes one running task at a time.
3. `MaxConcurrency` currently limits queued admission rather than true parallel running count.
4. Workflow persistence is not implemented.
5. Distributed execution is not implemented.
6. Task input payloads are represented as `InputJson` plus `InputType` rather than a stronger typed payload abstraction.

## Functional Requirements

## Workflow Definition And Validation

### FR-001 Workflow Specification Construction

The system shall allow a consumer to define a workflow declaratively using:

- `WorkflowSpecification`
- `TaskSpecification`
- `TaskDependencySpecification`

Rationale:

- workflow definition must be separable from runtime state
- the same specification must be reusable for validation, execution, and future persistence scenarios

### FR-002 Value Object Usage

The system shall represent workflow and task identity and task metadata labels using value objects rather than raw strings:

- `WorkflowId`
- `TaskId`
- `TaskType`
- `InputType`

Implications:

- consumer code must provide explicit construction from `string`
- consumer code may rely on string conversion when reading values back

### FR-003 Workflow Id Validation

The system shall reject a workflow specification whose `WorkflowId` is empty or whitespace.

Expected outcome:

- validation fails before execution starts

### FR-004 Task Id Validation

The system shall reject a task specification whose `TaskId` is empty or whitespace.

Expected outcome:

- validation fails before execution starts

### FR-005 Task Type Validation

The system shall reject a task specification whose `TaskType` value is empty or whitespace.

Expected outcome:

- validation fails before execution starts

### FR-006 Input Type Requirement

The system shall reject a task specification that provides `InputJson` without also providing `InputType`.

Expected outcome:

- validation fails before execution starts

### FR-007 Task Collection Requirement

The system shall reject a workflow specification with a missing task collection.

### FR-008 Dependency Collection Requirement

The system shall reject a workflow specification with a missing dependency collection.

### FR-009 Unique Task Ids

The system shall reject a workflow specification containing duplicate task ids.

Expected outcome:

- validation fails before runtime workflow creation completes

### FR-010 Dependency Endpoint Validation

The system shall reject a workflow specification whose dependencies reference missing prerequisite or dependent tasks.

### FR-011 Self Dependency Rejection

The system shall reject self-dependencies.

Example:

- `A -> A` is invalid

### FR-012 Duplicate Dependency Rejection

The system shall reject duplicate dependencies between the same prerequisite and dependent task ids.

Example:

- two copies of `A -> B` are invalid

### FR-013 Cycle Rejection In Static Specifications

The system shall reject a workflow specification whose dependency graph contains a cycle.

Expected outcome:

- validation fails before execution starts

### FR-014 MaxConcurrency Validation

The system shall reject non-positive `MaxConcurrency` values.

Accepted values:

- `null`
- positive integers

Rejected values:

- `0`
- negative integers

## Workflow Construction And Entry

### FR-015 Specification-Driven Workflow Construction

The system shall provide a public `Workflow.FromSpecification(...)` API that constructs a runtime workflow from a validated specification.

### FR-016 Internal Runtime Construction

The system shall prevent consumers from directly constructing runtime `Workflow` and `Task` instances through public constructors.

Rationale:

- runtime instances must be created through validated specifications
- engine-owned invariants must be preserved

### FR-017 Public Execution Entry Point

The system shall allow a consumer to execute a runtime workflow through `Workflow.RunToCompletion(...)`.

### FR-018 Optional Executor Argument

The system shall allow `Workflow.RunToCompletion(...)` to be called without explicitly supplying an executor.

Expected behavior:

- the built-in `DefaultTaskExecutor` is used

### FR-019 Optional Observer Argument

The system shall allow `Workflow.RunToCompletion(...)` to be called with or without an observer.

Expected behavior:

- when no observer is supplied, execution still proceeds successfully

## Status Model

### FR-020 Workflow Status Availability

The system shall expose a `WorkflowStatus` snapshot through `Workflow.Status`.

### FR-021 Task Status Availability

The system shall expose task status snapshots through `WorkflowStatus.TaskStatuses`.

### FR-022 Status Axes

The system shall model workflow and task state using the following orthogonal axes:

- `ExecutionPhase`
- `ExecutionOutcome`
- `ExecutionFailureKind`
- `ExecutionRecoverability`

### FR-023 Execution Phases

The system shall support the following lifecycle phases for tasks and workflows:

- `NotStarted`
- `ReadyToRun`
- `Queued`
- `Running`
- `Finished`

### FR-024 Execution Outcomes

The system shall support the following outcomes for tasks and workflows:

- `Pending`
- `Succeeded`
- `Canceled`
- `Failed`

### FR-025 Failure Kinds

The system shall support the following failure classifications:

- `None`
- `Transient`
- `Permanent`
- `Unknown`

### FR-026 Recoverability Values

The system shall support the following recoverability values:

- `AwaitingOutcome`
- `NoRecoveryNeeded`
- `Retryable`
- `Resumable`
- `RequiresIntervention`
- `NotRecoverable`
- `Unknown`

### FR-027 Status Payloads

The system shall allow workflow and task statuses to carry optional serializable payloads:

- `ExecutionOutput? Output`
- `ErrorInfo? Error`

### FR-028 Status Timestamps

The system shall record timestamps on workflow and task statuses for lifecycle transitions.

The currently exposed timestamps include:

- `Timestamp`
- `CreatedTimestamp`
- `QueuedTimestamp`
- `ReadyToRunTimestamp`
- `RunningTimestamp`
- `FinishedTimestamp`

### FR-029 Task Progress Fields

The system shall expose task progress counters in `TaskStatus`:

- `TotalSteps`
- `CompletedSteps`

Current implementation note:

- the default task flow uses a single-step model unless a future executor/runtime enhancement changes it

## Status Transition Semantics

### FR-030 Guarded Task Transitions

The system shall enforce legal task lifecycle transitions.

Examples of required behavior:

- a task may not move directly from `NotStarted` to `Running`
- a task may only move to `Queued` from `ReadyToRun`
- a task may only move to a terminal outcome from `Running`

### FR-031 Guarded Workflow Transitions

The system shall enforce legal workflow lifecycle transitions.

### FR-032 Task Completion Semantics

The system shall treat a task as complete for downstream scheduling only when it has finished and does not require restart-oriented recovery.

### FR-033 Restartable Task Semantics

The system shall treat retryable or resumable finished tasks as schedulable on a later rerun.

### FR-034 Dependency Satisfaction Semantics

The system shall not treat dependencies on restartable finished tasks as satisfied for future reruns.

## Recoverability Requirements

### FR-035 Default Recoverability Calculation

The system shall derive default recoverability from phase, outcome, and failure kind using `ExecutionRecoverabilityDefaults.From(...)`.

### FR-036 Baseline Recoverability Mappings

The system shall apply the following baseline default mappings:

- non-finished state -> `AwaitingOutcome`
- succeeded -> `NoRecoveryNeeded`
- canceled -> `Retryable`
- failed + transient -> `Retryable`
- failed + permanent -> `NotRecoverable`
- failed + unknown -> `RequiresIntervention`
- failed + none -> `RequiresIntervention`

### FR-037 Consumer Visibility Of Recoverability

The system shall expose `ExecutionRecoverability` to consumers as part of the public status and result model.

### FR-038 Override Support In Terminal Results

The system shall allow terminal task results to specify recoverability explicitly when supported by the result type.

Current rules:

- canceled results may specify terminal recoverability
- failed results may specify terminal recoverability
- succeeded results may not specify recoverability

### FR-039 Terminal Recoverability Validation

The system shall reject canceled or failed task results that specify non-terminal recoverability values.

## Execution Boundary Requirements

### FR-040 Executor Contract

The system shall execute task-specific work through a consumer-implemented `ITaskExecutor` interface.

### FR-041 Execution Context Contract

The system shall provide the executor with an `IExecutionContext` containing:

- `WorkflowId`
- `TaskId`
- `TaskSpecification`
- `DependencyStatuses`
- `DependencyOutputs`

### FR-042 Dependency Output Nullability

The system shall expose dependency outputs as nullable values:

- `IReadOnlyDictionary<TaskId, ExecutionOutput?>`

Rationale:

- dependency status may exist even when output is absent

### FR-043 Execution Context Read-Only Semantics

The system shall prevent the executor from directly mutating engine-owned state through the execution context.

### FR-044 Default Executor Behavior

The system shall provide a built-in `DefaultTaskExecutor`.

Required behavior:

- validate the current task specification
- when `InputJson` is absent, emit the task type as output
- when `InputJson` is present, emit `TaskType(InputType): InputJson`

### FR-045 Default Executor Input Validation Consistency

The default executor shall not invent an `InputType` when `InputJson` is present.

Required behavior:

- it must rely on specification validation instead of applying a hidden fallback

## Task Result Requirements

### FR-046 Factory-Based Result Creation

The system shall create task execution results through factory methods rather than a public positional constructor.

Required factory methods:

- `TaskExecutionResult.Succeeded(...)`
- `TaskExecutionResult.Canceled(...)`
- `TaskExecutionResult.Failed(...)`

### FR-047 Success Results

The system shall allow success results to carry:

- optional output
- optional spawned tasks
- optional added dependencies

### FR-048 Canceled Results

The system shall allow canceled results to carry:

- optional output
- optional error
- required effective terminal recoverability, defaulting to `Retryable`

### FR-049 Failed Results

The system shall allow failed results to carry:

- required failure kind other than `None`
- optional output
- optional error
- optional terminal recoverability override

### FR-050 Runtime Mutation Restriction

The system shall reject runtime mutations on non-success results.

### FR-051 Success Result Validation

The system shall reject success results that carry:

- a failure kind other than `None`
- an explicit recoverability value

### FR-052 Canceled Result Validation

The system shall reject canceled results that carry a failure kind other than `None`.

### FR-053 Failed Result Validation

The system shall reject failed results whose failure kind is `None`.

## Static Workflow Execution

### FR-054 Static DAG Support

The system shall execute workflows whose full task set and dependency set are known before execution begins.

### FR-055 Dependency-Safe Scheduling

The system shall schedule a task only when all of its dependencies are satisfied according to the current dependency-completion rules.

### FR-056 Terminal Success Completion

The system shall mark the workflow succeeded when all tasks have completed successfully and no restartable tasks remain.

### FR-057 Terminal Workflow Cancelation

The system shall mark the workflow canceled when a task returns a canceled result.

### FR-058 Terminal Workflow Failure

The system shall mark the workflow failed when a task returns a failed result.

### FR-059 Blocked Workflow Detection

The system shall fail execution when unfinished tasks remain but no ready tasks exist.

Expected failure meaning:

- the graph may be cyclic
- the graph may be blocked by unsatisfied dependencies

## Dynamic Workflow Execution

### FR-060 Dynamic Spawn Support

The system shall allow a succeeded task result to create additional tasks at runtime.

### FR-061 Spawned Task Validation

The system shall validate each spawned task as a normal `TaskSpecification` before adding it to the runtime graph.

### FR-062 Spawn Provenance

The system shall populate `SpawnedByTaskId` with the current task id when a spawned task omits it.

### FR-063 Dynamic Dependency Addition

The system shall allow a succeeded task result to add dependencies at runtime.

### FR-064 Dynamic Join Blocking

The system shall allow a succeeded task result to express dynamic joins by adding ordinary runtime dependencies to a join task.

### FR-065 Dynamic Dependency Target Restriction

The system shall allow runtime dependencies to target only tasks that have not started execution.

### FR-066 Dynamic Cycle Prevention

The system shall reject runtime dependencies that would introduce a cycle.

### FR-067 Dynamic Duplicate Dependency Prevention

The system shall reject runtime dependencies that duplicate an existing dependency.

### FR-068 Dynamic Self Dependency Prevention

The system shall reject runtime self-dependencies.

### FR-069 Dynamic Missing Endpoint Prevention

The system shall reject runtime dependencies that reference tasks not present in the runtime graph.

### FR-070 Queue Re-Blocking After Dynamic Dependency Addition

The system shall support the case where a task that was already admitted to the queue becomes blocked again due to runtime dependency addition.

Required effect:

- stale queued entries shall not incorrectly execute blocked tasks

## Concurrency And Scheduling

### FR-073 Workflow-Level Concurrency Control

The system shall support an optional workflow-level `MaxConcurrency` setting.

### FR-074 Current Synchronous Interpretation Of MaxConcurrency

In the current synchronous implementation, the system shall interpret `MaxConcurrency` as a limit on how many ready tasks are admitted into the queued set at once.

### FR-075 No Async Requirement In Current Implementation

The system shall not require asynchronous task execution in the current implementation.

### FR-076 Live Graph Rescan

The system shall rescan the live graph for newly ready tasks after applying each task result and any runtime mutations.

## Retry And Rerun Behavior

### FR-077 Retryable Failure Rerun

The system shall allow a workflow to be run again after a retryable failed task and shall rerun that task before executing dependent tasks.

### FR-078 Retryable Cancelation Rerun

The system shall allow a workflow to be run again after a retryable canceled task and shall rerun that task.

### FR-079 Rerun Completion Semantics

The system shall, on rerun, update the workflow and task statuses to reflect the latest successful completion when retryable tasks eventually succeed.

## Observer And Reporting Requirements

### FR-080 Observer Interface

The system shall expose `IWorkflowObserver` with callbacks for:

- task transitions
- workflow transitions
- task additions
- dependency additions

### FR-081 Workflow Transition Reporting

The system shall notify observers when workflow phase transitions occur.

### FR-082 Task Transition Reporting

The system shall notify observers when task phase transitions occur.

### FR-083 Runtime Task Addition Reporting

The system shall notify observers when runtime mutation adds a task.

### FR-084 Runtime Dependency Addition Reporting

The system shall notify observers when runtime mutation adds a dependency.

### FR-085 Built-In Text Observer

The system shall provide a built-in `TextWriterWorkflowObserver` that writes human-readable lines for:

- workflow transitions
- task transitions
- task additions
- dependency additions

### FR-086 Status Formatter Utilities

The system shall provide formatter utilities for task and workflow status display lines:

- `TaskStatusFormatter`
- `WorkflowStatusFormatter`

### FR-087 Observer Failure Isolation

The system shall isolate observer failures from workflow execution when emitting engine-generated notifications.

Required effect:

- observer exceptions shall not crash workflow execution during internal transition notification

## Output And Error Requirements

### FR-088 Serializable Output Model

The system shall represent task and workflow output through `ExecutionOutput` rather than raw implementation-specific objects.

### FR-089 Built-In Text Output Type

The system shall provide `TextExecutionOutput` as a built-in concrete output type.

### FR-090 JSON Serialization Support

The system shall allow execution outputs to serialize themselves to JSON through `ExecutionOutput.ToJson()`.

### FR-091 Serializable Error Model

The system shall represent task and workflow errors using serializable `ErrorInfo` values instead of storing raw exceptions on status records.

### FR-092 Exception Conversion Support

The system shall provide `ErrorInfo.FromException(...)` to convert exceptions into serializable error payloads.

## Functional Exclusions

The current implementation is not required to provide:

1. Distributed task execution.
2. Durable workflow persistence.
3. Async executor signatures.
4. Built-in cron or event-driven triggering.
5. Built-in multi-process or multi-machine coordination.

## Acceptance Summary

The system shall be considered functionally complete for the current implementation when all of the following are true:

1. Consumers can define and validate static workflows.
2. Consumers can run workflows through `Workflow.FromSpecification(...)` and `Workflow.RunToCompletion(...)`.
3. Executors can inspect task input and dependency context through `IExecutionContext`.
4. Executors can return succeeded, canceled, and failed results using the factory API.
5. The engine correctly enforces status transitions and result invariants.
6. Retryable canceled or failed tasks can be rerun through subsequent workflow executions.
7. Succeeded tasks can spawn tasks and add dependencies at runtime.
8. Observers can receive transitions and mutation notifications.
9. Consumers can inspect final workflow and task statuses, outputs, errors, timestamps, and recoverability.

## Relationship To Other Documents

This document should be read alongside:

- `DagWorkflow-Design.md` for architectural intent and structural boundaries
- `DagWorkflow-Developer-Guide.md` for consumer-oriented examples and usage patterns

If a future change causes a mismatch between those documents and the implemented behavior, this functional requirements document should be updated to describe the required behavior that the implementation is expected to satisfy.