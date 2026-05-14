namespace AniNest.Features.Metadata;

public sealed record MetadataStatusSummary(
    int NeedsMetadataCount,
    int QueuedCount,
    int ScrapingCount,
    int ReadyCount,
    int NeedsReviewCount,
    int DisabledCount,
    int NetworkErrorCount,
    int NoMatchCount,
    int ProviderErrorCount);
