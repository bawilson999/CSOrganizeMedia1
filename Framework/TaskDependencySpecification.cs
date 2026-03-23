namespace OrganizeMedia.Framework;

public readonly record struct TaskDependencySpecification(
    TaskSpecificationId PrerequisiteTaskSpecificationId,
    TaskSpecificationId DependentTaskSpecificationId);