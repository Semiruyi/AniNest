using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;
using AniNest.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class MetadataReviewService : IMetadataReviewService
{
    private readonly IMetadataRuntimeBootstrapService _bootstrap;
    private readonly IMetadataRuntimeStateService _state;
    private readonly IMetadataReviewStore _reviewStore;
    private readonly IMetadataAssetService _assets;
    private readonly IMetadataStore _legacyStore;
    private readonly IAnimeMetadataProvider _provider;
    private readonly ILogger<MetadataReviewService> _logger;

    public MetadataReviewService(
        IMetadataRuntimeBootstrapService bootstrap,
        IMetadataRuntimeStateService state,
        IMetadataReviewStore reviewStore,
        IMetadataAssetService assets,
        IMetadataStore legacyStore,
        IAnimeMetadataProvider provider,
        ILogger<MetadataReviewService> logger)
    {
        _bootstrap = bootstrap;
        _state = state;
        _reviewStore = reviewStore;
        _assets = assets;
        _legacyStore = legacyStore;
        _provider = provider;
        _logger = logger;
    }

    public Task<IReadOnlyList<MetadataReviewDto>> GetReviewQueueAsync(CancellationToken cancellationToken = default)
    {
        _bootstrap.EnsureInitialized();
        return Task.FromResult<IReadOnlyList<MetadataReviewDto>>(
            _reviewStore.GetAll()
                .OrderByDescending(item => item.UpdatedAtUtc)
                .Select(MapReview)
                .ToArray());
    }

    public Task<MetadataReviewDto?> GetReviewByFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _bootstrap.EnsureInitialized();
        return Task.FromResult(_reviewStore.GetByFolderId(folderId) is { } review ? MapReview(review) : null);
    }

    public async Task ConfirmReviewAsync(string folderId, string sourceId, CancellationToken cancellationToken = default)
    {
        _bootstrap.EnsureInitialized();
        var record = _state.RequireRecord(folderId);
        var review = _reviewStore.GetByFolderId(folderId)
            ?? throw new KeyNotFoundException($"Metadata review for folder '{folderId}' was not found.");
        if (!review.Candidates.Any(candidate => string.Equals(candidate.SourceId, sourceId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Metadata review candidate '{sourceId}' was not found for folder '{folderId}'.");

        var detail = await _provider.GetSubjectAsync(sourceId, cancellationToken);
        var payload = new FolderMetadataPayload(
            folderId,
            sourceId,
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
            DateTime.UtcNow);
        var assets = await _assets.SaveResolvedPayloadAsync(
            folderId,
            payload,
            record.MetadataFilePath,
            record.PosterFilePath,
            cancellationToken);

        var completedRecord = record with
        {
            State = MetadataState.Ready,
            FailureKind = MetadataFailureKind.None,
            SourceId = sourceId,
            MetadataFilePath = assets.PayloadPath,
            PosterFilePath = assets.PosterFilePath,
            LastSucceededAtUtc = DateTime.UtcNow
        };
        _state.SaveRecord(completedRecord);
        _reviewStore.Delete(folderId);
        _legacyStore.Save(new MetadataDto(
            folderId,
            assets.Payload.Title,
            assets.Payload.OriginalTitle,
            assets.Payload.Summary,
            assets.Payload.Tags,
            assets.Payload.LocalPosterPath,
            null,
            assets.Payload.EpisodeCount,
            assets.Payload.Source,
            completedRecord.State,
            completedRecord.FailureKind,
            assets.Payload.AirDate,
            assets.Payload.Year,
            assets.Payload.Rating));
        _logger.LogInformation(
            "Metadata review confirmed manually. FolderId={FolderId}, SourceId={SourceId}, SuggestedSourceId={SuggestedSourceId}, PosterFilePath={PosterFilePath}",
            folderId,
            sourceId,
            review.SuggestedSourceId,
            completedRecord.PosterFilePath ?? "(null)");
        _state.PublishFolderState(folderId);
    }

    public Task RejectReviewCandidateAsync(string folderId, string sourceId, CancellationToken cancellationToken = default)
    {
        _bootstrap.EnsureInitialized();
        var review = _reviewStore.GetByFolderId(folderId)
            ?? throw new KeyNotFoundException($"Metadata review for folder '{folderId}' was not found.");

        var remainingCandidates = review.Candidates
            .Where(candidate => !string.Equals(candidate.SourceId, sourceId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (remainingCandidates.Length == review.Candidates.Count)
            throw new InvalidOperationException($"Metadata review candidate '{sourceId}' was not found for folder '{folderId}'.");

        var rejectedSourceIds = review.RejectedSourceIds
            .Concat([sourceId])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var nextSuggested = remainingCandidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.SourceId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var updatedReview = review with
        {
            SuggestedSourceId = nextSuggested?.SourceId,
            SuggestedTitle = nextSuggested?.DisplayTitle ?? nextSuggested?.MatchedTitle,
            Reason = remainingCandidates.Length == 0 ? "review.candidates_exhausted" : "review.candidate_rejected",
            Candidates = remainingCandidates,
            RejectedSourceIds = rejectedSourceIds,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _reviewStore.Save(updatedReview);

        _logger.LogInformation(
            "Metadata review candidate rejected manually. FolderId={FolderId}, SourceId={SourceId}, RemainingCandidates={RemainingCandidates}",
            folderId,
            sourceId,
            remainingCandidates.Length);
        _state.PublishFolderState(folderId);
        return Task.CompletedTask;
    }

    private static MetadataReviewDto MapReview(MetadataReviewRecord review)
        => new(
            review.FolderId,
            review.FolderName,
            review.State,
            review.FailureKind,
            review.SuggestedSourceId,
            review.SuggestedTitle,
            review.Reason,
            review.Candidates.Select(candidate => new MetadataReviewCandidateDto(
                candidate.SourceId,
                candidate.MatchedTitle,
                candidate.OriginalTitle,
                candidate.DisplayTitle,
                candidate.Score,
                candidate.ConfidenceLevel,
                candidate.Reasons))
                .ToArray(),
            review.RejectedSourceIds,
            review.UpdatedAtUtc);
}
