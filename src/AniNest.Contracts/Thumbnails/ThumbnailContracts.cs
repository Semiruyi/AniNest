using AniNest.Core.Enums;

namespace AniNest.Contracts.Thumbnails;

public sealed record ThumbnailStatusDto(
    string TargetId,
    ThumbnailState State,
    double ProgressPercent,
    string? ImagePath,
    DateTimeOffset? UpdatedAtUtc);
