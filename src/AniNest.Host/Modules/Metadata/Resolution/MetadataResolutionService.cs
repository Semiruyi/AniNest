using AniNest.Application.Metadata;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal sealed class MetadataResolutionService : IMetadataResolutionService
{
    public MetadataResolutionResult Resolve(
        MetadataRecord record,
        MetadataPreparedContext context,
        MetadataAcquisitionResult acquisition,
        MetadataConfidenceResult confidence)
    {
        if (confidence.BestCandidate is { Level: MetadataConfidenceLevel.High or MetadataConfidenceLevel.Medium } trusted &&
            trusted.Candidate.Detail is { } detail)
        {
            return new MetadataResolutionResult(
                MetadataState.Ready,
                MetadataFailureKind.None,
                new FolderMetadataPayload(
                    record.FolderId,
                    trusted.Candidate.SourceId,
                    detail.Title,
                    detail.OriginalTitle,
                    detail.Summary,
                    detail.PosterUrl,
                    null,
                    detail.AirDate,
                    detail.Year,
                    detail.Rating,
                    detail.EpisodeCount,
                    detail.Tags,
                    detail.Source,
                    DateTime.UtcNow),
                trusted.Candidate.SourceId,
                detail.PosterUrl,
                "confidence.accepted");
        }

        if (!acquisition.SearchSucceeded)
        {
            var failureKind = ResolveFailureKind(acquisition.FailureReason);
            return new MetadataResolutionResult(
                MetadataState.NeedsReview,
                failureKind,
                null,
                null,
                null,
                acquisition.FailureReason ?? "acquisition.failed");
        }

        return new MetadataResolutionResult(
            MetadataState.NeedsReview,
            MetadataFailureKind.NoMatch,
            null,
            null,
            null,
            "confidence.rejected");
    }

    private static MetadataFailureKind ResolveFailureKind(string? failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            return MetadataFailureKind.ProviderError;

        if (failureReason.StartsWith("search_failed:", StringComparison.OrdinalIgnoreCase))
            return MetadataFailureKind.NetworkError;

        if (string.Equals(failureReason, "detail_failed", StringComparison.OrdinalIgnoreCase))
            return MetadataFailureKind.ProviderError;

        if (string.Equals(failureReason, "no_match", StringComparison.OrdinalIgnoreCase))
            return MetadataFailureKind.NoMatch;

        return MetadataFailureKind.ProviderError;
    }
}
