namespace AniNest.Application.Metadata;

public sealed record MetadataKeywordPlan(
    string PrimaryKeyword,
    string? SeasonAwareKeyword,
    string? SimplifiedKeyword,
    string BaseTitle,
    int? SeasonNumber,
    int? YearHint,
    bool IsAmbiguousShortKeyword,
    bool IsMovieLike);
