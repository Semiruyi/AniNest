using AniNest.Core.Enums;

namespace AniNest.Contracts.Thumbnails;

public sealed record ThumbnailStatusDto(
    string TargetId,
    ThumbnailState State,
    double ProgressPercent,
    string? ImagePath,
    DateTimeOffset? UpdatedAtUtc);

public sealed record ThumbnailFolderSummaryDto(
    string FolderId,
    int Total,
    int Pending,
    int Generating,
    int Ready,
    int Failed,
    double CompletionPercent,
    DateTimeOffset? UpdatedAtUtc);

public sealed record ThumbnailProcessingResultDto(
    string FolderId,
    int ProcessedCount,
    ThumbnailFolderSummaryDto Summary,
    IReadOnlyList<string> TargetIds);
