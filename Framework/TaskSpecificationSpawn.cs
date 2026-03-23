namespace OrganizeMedia.Framework;

public sealed record TaskSpecificationSpawn(
    string SpawnKey,
    TaskSpecificationId TaskSpecificationId,
    InputType? InputType = null,
    string? InputJson = null);