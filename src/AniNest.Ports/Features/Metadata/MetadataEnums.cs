namespace AniNest.Features.Metadata;

public enum MetadataState
{
    NeedsMetadata,
    Queued,
    Scraping,
    Ready,
    NeedsReview,
    Disabled
}

public enum MetadataFailureKind
{
    None,
    NoMatch,
    NetworkError,
    ProviderError
}
