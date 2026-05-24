namespace AniNest.Host.Modules;

internal sealed record MetadataAcquisitionResult(
    bool SearchSucceeded,
    IReadOnlyList<MetadataAcquisitionCandidate> Candidates,
    string? FailureReason);
