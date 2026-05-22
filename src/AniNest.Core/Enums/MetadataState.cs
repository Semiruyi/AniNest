namespace AniNest.Core.Enums;

public enum MetadataState
{
    Unknown = 0,
    NeedsMetadata = 1,
    Queued = 2,
    Scraping = 3,
    Ready = 4,
    NeedsReview = 5,
    Disabled = 6
}
