using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal sealed record MetadataAcquisitionCandidate(
    string SourceId,
    string? MatchedTitle,
    ProviderSubjectDetail? Detail);
