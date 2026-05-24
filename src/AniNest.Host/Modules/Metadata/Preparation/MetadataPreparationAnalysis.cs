using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal sealed record MetadataPreparationAnalysis(
    string SearchSeed,
    string BaseTitle,
    IReadOnlyList<string> Aliases,
    MetadataKeywordPlan KeywordPlan,
    int? SeasonNumber,
    int? YearHint,
    bool IsMovieLike);
