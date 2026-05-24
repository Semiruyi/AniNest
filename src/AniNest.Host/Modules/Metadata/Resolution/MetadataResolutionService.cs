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
            return new MetadataResolutionResult(
                MetadataState.NeedsReview,
                MetadataFailureKind.NoMatch,
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
}
