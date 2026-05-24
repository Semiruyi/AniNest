namespace AniNest.Host.Modules;

internal sealed record MetadataConfidenceCandidate(
    MetadataAcquisitionCandidate Candidate,
    double Score,
    MetadataConfidenceLevel Level,
    IReadOnlyList<string> Reasons);
