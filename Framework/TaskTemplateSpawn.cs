namespace OrganizeMedia.Framework;

public sealed record TaskTemplateSpawn(
    string SpawnKey,
    TaskId TemplateTaskId,
    InputType? InputType = null,
    string? InputJson = null);