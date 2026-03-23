namespace OrganizeMedia.Framework;

public sealed record TaskSpawnRequest(
    string SpawnReference,
    TaskSpecificationId TaskSpecificationId,
    InputType? InputType = null,
    string? InputJson = null);