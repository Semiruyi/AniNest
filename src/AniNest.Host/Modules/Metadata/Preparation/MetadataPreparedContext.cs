using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal sealed record MetadataPreparedContext(
    MetadataRecord Record,
    MetadataFolderRef Folder,
    MetadataKeywordPlan KeywordPlan,
    string NormalizedTitle,
    IReadOnlyList<string> Aliases,
    int? SeasonNumber,
    int? YearHint,
    bool IsMovieLike);
