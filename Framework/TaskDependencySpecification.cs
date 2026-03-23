namespace OrganizeMedia.Framework;

public readonly record struct TaskDependencySpecification(
    TaskTemplateId PrerequisiteTaskTemplateId,
    TaskTemplateId DependentTaskTemplateId);