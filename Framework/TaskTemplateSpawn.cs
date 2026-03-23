namespace OrganizeMedia.Framework;

public sealed record TaskTemplateSpawn(
    string SpawnKey,
    TaskSpecificationId TaskSpecificationId,
    InputType? InputType = null,
    string? InputJson = null);