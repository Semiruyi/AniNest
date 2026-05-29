namespace AniNest.Application.Metadata;

public sealed record ProviderSubjectDetail(
    string SourceId,
    string? Title,
    string? OriginalTitle,
    string? Summary,
    string? PosterUrl,
    string? AirDate,
    int? Year,
    double? Rating,
    int? EpisodeCount,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Tags,
    string Source);
