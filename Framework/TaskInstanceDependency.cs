namespace OrganizeMedia.Framework;

public sealed record TaskInstanceDependency(
    TaskNodeReference Prerequisite,
    TaskNodeReference Dependent);