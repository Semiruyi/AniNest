namespace AniNest.Application.Metadata;

public sealed record ProviderSearchResult(
    string SourceId,
    string? MatchedTitle,
    string? OriginalTitle,
    int? Year,
    string Source);
