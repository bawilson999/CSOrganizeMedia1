namespace OrganizeMedia.Framework;

public sealed record TaskTemplateSpawn(
    string SpawnKey,
    TaskTemplateId TaskTemplateId,
    InputType? InputType = null,
    string? InputJson = null);