namespace OrganizeMedia.Framework;

public readonly record struct TaskDependencySpecification(
    TaskId PrerequisiteTaskId,
    TaskId DependentTaskId)
{
}