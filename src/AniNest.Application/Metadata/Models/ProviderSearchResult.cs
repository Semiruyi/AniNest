namespace AniNest.Application.Metadata;

public sealed record ProviderSearchResult(
    bool IsMatch,
    string? SourceId,
    string? MatchedTitle,
    string? FailureReason);
