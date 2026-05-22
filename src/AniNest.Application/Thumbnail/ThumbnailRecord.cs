using AniNest.Core.Enums;

namespace AniNest.Application.Thumbnail;

public sealed record ThumbnailRecord(
    string FolderId,
    string TargetId,
    ThumbnailState State,
    double ProgressPercent,
    string? ImagePath,
    DateTimeOffset? UpdatedAtUtc);
