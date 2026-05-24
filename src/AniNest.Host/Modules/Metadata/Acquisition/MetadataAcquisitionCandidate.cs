using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal sealed record MetadataAcquisitionCandidate(
    string SourceId,
    string? MatchedTitle,
    string? OriginalTitle,
    int? Year,
    int HitCount,
    int BestRank,
    ProviderSubjectDetail? Detail);
