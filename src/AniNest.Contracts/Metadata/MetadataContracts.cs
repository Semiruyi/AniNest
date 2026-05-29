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
    MetadataFailureKind FailureKind,
    string? AirDate = null,
    int? Year = null,
    double? Rating = null);

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

public sealed record MetadataDebugMatchRequest(
    string FolderName,
    string? ParentFolderName = null,
    IReadOnlyList<string>? VideoFiles = null);

public sealed record MetadataDebugPreparedDto(
    string SearchSeed,
    string NormalizedTitle,
    IReadOnlyList<string> Aliases,
    string PrimaryKeyword,
    string? SeasonAwareKeyword,
    string? SimplifiedKeyword,
    string BaseTitle,
    int? SeasonNumber,
    int? YearHint,
    bool IsMovieLike);

public sealed record MetadataDebugCandidateDto(
    string SourceId,
    string? MatchedTitle,
    string? OriginalTitle,
    string? DetailTitle,
    string? DetailOriginalTitle,
    int? Year,
    int? DetailYear,
    int HitCount,
    int BestRank,
    double Score,
    string ConfidenceLevel,
    IReadOnlyList<string> Reasons);

public sealed record MetadataDebugMatchResponse(
    MetadataDebugPreparedDto Prepared,
    IReadOnlyList<string> SearchKeywords,
    bool SearchSucceeded,
    string? FailureReason,
    MetadataDebugCandidateDto? BestCandidate,
    IReadOnlyList<MetadataDebugCandidateDto> Candidates);
