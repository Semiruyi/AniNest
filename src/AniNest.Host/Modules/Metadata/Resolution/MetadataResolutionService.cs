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
                "confidence.accepted",
                null);
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
                acquisition.FailureReason ?? "acquisition.failed",
                BuildReviewRecord(record, confidence, failureKind, acquisition.FailureReason ?? "acquisition.failed"));
        }

        return new MetadataResolutionResult(
            MetadataState.NeedsReview,
            MetadataFailureKind.NoMatch,
            null,
            null,
            null,
            "confidence.rejected",
            BuildReviewRecord(record, confidence, MetadataFailureKind.NoMatch, "confidence.rejected"));
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

    private static MetadataReviewRecord BuildReviewRecord(
        MetadataRecord record,
        MetadataConfidenceResult confidence,
        MetadataFailureKind failureKind,
        string reason)
    {
        return new MetadataReviewRecord(
            record.FolderId,
            record.FolderName,
            MetadataState.NeedsReview,
            failureKind,
            confidence.BestCandidate?.Candidate.SourceId,
            confidence.BestCandidate?.Candidate.Detail?.Title ?? confidence.BestCandidate?.Candidate.MatchedTitle,
            reason,
            confidence.Candidates.Select(candidate => new MetadataReviewCandidate(
                candidate.Candidate.SourceId,
                candidate.Candidate.MatchedTitle,
                candidate.Candidate.OriginalTitle,
                candidate.Candidate.Detail?.Title ?? candidate.Candidate.MatchedTitle,
                candidate.Score,
                candidate.Level.ToString(),
                candidate.Reasons))
                .ToArray(),
            [],
            DateTime.UtcNow);
    }
}
