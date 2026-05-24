namespace AniNest.Host.Modules;

internal sealed record MetadataConfidenceResult(
    MetadataConfidenceCandidate? BestCandidate,
    IReadOnlyList<MetadataConfidenceCandidate> Candidates);
