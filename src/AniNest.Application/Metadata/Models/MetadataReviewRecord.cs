using AniNest.Core.Enums;

namespace AniNest.Application.Metadata;

public sealed record MetadataReviewCandidate(
    string SourceId,
    string? MatchedTitle,
    string? OriginalTitle,
    string? DisplayTitle,
    double Score,
    string ConfidenceLevel,
    IReadOnlyList<string> Reasons);

public sealed record MetadataReviewRecord(
    string FolderId,
    string FolderName,
    MetadataState State,
    MetadataFailureKind FailureKind,
    string? SuggestedSourceId,
    string? SuggestedTitle,
    string? Reason,
    IReadOnlyList<MetadataReviewCandidate> Candidates,
    IReadOnlyList<string> RejectedSourceIds,
    DateTime UpdatedAtUtc);
