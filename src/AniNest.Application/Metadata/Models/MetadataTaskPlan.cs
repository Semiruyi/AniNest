namespace AniNest.Application.Metadata;

public sealed record MetadataTaskPlan(
    string FolderId,
    MetadataTaskReason Reason,
    int Priority,
    bool BypassCooldown);
