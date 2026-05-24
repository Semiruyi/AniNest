using AniNest.Core.Enums;

namespace AniNest.Contracts.Metadata;

public sealed record MetadataDto(
    string FolderId,
    string? Title,
    string? OriginalTitle,
    string? Summary,
    IReadOnlyList<string> Tags,
    string? PosterPath,
    string? Season,
    int? EpisodeCount,
    string? Source,
    MetadataState State,
    MetadataFailureKind FailureKind);

public sealed record MetadataStatusSummaryDto(
    int NeedsMetadata,
    int Queued,
    int Scraping,
    int Ready,
    int NeedsReview,
    int Disabled,
    int NetworkError,
    int NoMatch,
    int ProviderError);

public sealed record RetryFailedMetadataRequest(
    bool IncludeNoMatch);

public sealed record MetadataProcessingResultDto(
    int ProcessedCount,
    IReadOnlyList<string> FolderIds);

public sealed record MetadataReviewCandidateDto(
    string SourceId,
    string? MatchedTitle,
    string? OriginalTitle,
    string? DisplayTitle,
    double Score,
    string ConfidenceLevel,
    IReadOnlyList<string> Reasons);

public sealed record MetadataReviewDto(
    string FolderId,
    string FolderName,
    MetadataState State,
    MetadataFailureKind FailureKind,
    string? SuggestedSourceId,
    string? SuggestedTitle,
    string? Reason,
    IReadOnlyList<MetadataReviewCandidateDto> Candidates,
    IReadOnlyList<string> RejectedSourceIds,
    DateTime UpdatedAtUtc);

public sealed record ConfirmMetadataReviewRequest(
    string SourceId);

public sealed record RejectMetadataReviewCandidateRequest(
    string SourceId);
