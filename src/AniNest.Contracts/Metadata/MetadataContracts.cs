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
