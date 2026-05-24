namespace AniNest.Host.Modules;

internal sealed record MetadataPreparationCandidate(
    string RawInput,
    string BaseTitle,
    int? SeasonNumber,
    int? YearHint,
    bool IsMovieLike,
    int Score);
