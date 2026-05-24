using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;
using AniNest.Core.Enums;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class MetadataLifecycleService : IMetadataLifecycleService
{
    private readonly IMetadataRuntimeStateService _state;
    private readonly IMetadataReviewStore _reviewStore;
    private readonly IMetadataPayloadRepository _payloadRepository;
    private readonly IMetadataStore _legacyStore;
    private readonly IAnimeMetadataProvider _provider;
    private readonly IMetadataTaskPlanner _planner;
    private readonly IMetadataTaskQueue _queue;
    private readonly ILogger<MetadataLifecycleService> _logger;

    public MetadataLifecycleService(
        IMetadataRuntimeStateService state,
        IMetadataReviewStore reviewStore,
        IMetadataPayloadRepository payloadRepository,
        IMetadataStore legacyStore,
        IAnimeMetadataProvider provider,
        IMetadataTaskPlanner planner,
        IMetadataTaskQueue queue,
        ILogger<MetadataLifecycleService> logger)
    {
        _state = state;
        _reviewStore = reviewStore;
        _payloadRepository = payloadRepository;
        _legacyStore = legacyStore;
        _provider = provider;
        _planner = planner;
        _queue = queue;
        _logger = logger;
    }

    public Task<MetadataDto?> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _state.EnsureInitialized();
        return Task.FromResult(_state.GetMetadata(folderId));
    }

    public Task<MetadataStatusSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        _state.EnsureInitialized();
        return Task.FromResult(_state.BuildSummary());
    }

    public Task<IReadOnlyList<MetadataReviewDto>> GetReviewQueueAsync(CancellationToken cancellationToken = default)
    {
        _state.EnsureInitialized();
        return Task.FromResult<IReadOnlyList<MetadataReviewDto>>(
            _reviewStore.GetAll()
                .OrderByDescending(item => item.UpdatedAtUtc)
                .Select(MapReview)
                .ToArray());
    }

    public Task<MetadataReviewDto?> GetReviewByFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _state.EnsureInitialized();
        return Task.FromResult(_reviewStore.GetByFolderId(folderId) is { } review ? MapReview(review) : null);
    }

    public async Task ConfirmReviewAsync(string folderId, string sourceId, CancellationToken cancellationToken = default)
    {
        _state.EnsureInitialized();
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
        var payloadPath = MetadataStoragePathCodec.GetPayloadPath(folderId);
        _payloadRepository.Save(payloadPath, payload);

        var completedRecord = record with
        {
            State = MetadataState.Ready,
            FailureKind = MetadataFailureKind.None,
            SourceId = sourceId,
            MetadataFilePath = payloadPath,
            PosterFilePath = null,
            LastSucceededAtUtc = DateTime.UtcNow
        };
        _state.SaveRecord(completedRecord);
        _reviewStore.Delete(folderId);
        _legacyStore.Save(new MetadataDto(
            folderId,
            payload.Title,
            payload.OriginalTitle,
            payload.Summary,
            payload.Tags,
            payload.LocalPosterPath,
            null,
            payload.EpisodeCount,
            payload.Source,
            completedRecord.State,
            completedRecord.FailureKind));
        _logger.LogInformation(
            "Metadata review confirmed manually. FolderId={FolderId}, SourceId={SourceId}, SuggestedSourceId={SuggestedSourceId}",
            folderId,
            sourceId,
            review.SuggestedSourceId);
        _state.PublishFolderState(folderId);
    }

    public Task RejectReviewCandidateAsync(string folderId, string sourceId, CancellationToken cancellationToken = default)
    {
        _state.EnsureInitialized();
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

    public Task SyncLibrarySnapshotAsync(IReadOnlyList<MetadataFolderRef> folders, CancellationToken cancellationToken = default)
    {
        _state.EnsureInitialized();
        _logger.LogInformation(
            "Metadata snapshot sync started. FolderCount={FolderCount}, RecordCount={RecordCount}",
            folders.Count,
            _state.GetAllRecords().Count);
        _state.NormalizeTransientStates();
        var currentRecords = _state.GetAllRecords();
        var plan = _planner.BuildLibrarySyncPlan(currentRecords, folders);

        foreach (var recordId in plan.RecordIdsToDelete)
        {
            _logger.LogInformation(
                "Metadata record removed because folder is no longer in library snapshot. FolderId={FolderId}",
                recordId);
            _state.DeleteRecord(recordId);
        }

        var existingIds = currentRecords.Select(item => item.FolderId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var record in plan.RecordsToUpsert)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _state.SaveRecord(record);
            if (!existingIds.Contains(record.FolderId))
            {
                _logger.LogInformation(
                    "Metadata placeholder record created. FolderId={FolderId}, FolderName={FolderName}",
                    record.FolderId,
                    record.FolderName);
            }
            else if (record.State == MetadataState.NeedsMetadata)
            {
                _logger.LogInformation(
                    "Metadata folder eligible after snapshot sync. FolderId={FolderId}",
                    record.FolderId);
            }
        }

        EnqueuePlans(plan.TaskPlans);
        _logger.LogInformation("Metadata snapshot sync completed.");
        _state.PublishSummaryChanged();
        return Task.CompletedTask;
    }

    public Task RefreshFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        _state.EnsureInitialized();
        _logger.LogInformation("Metadata refresh requested. FolderId={FolderId}", folderId);
        var plan = _planner.BuildRefreshPlan(_state.RequireRecord(folderId));
        _state.SaveRecord(plan.Record);
        if (plan.TaskPlan is not null)
            EnqueuePlan(plan.TaskPlan);
        _state.PublishFolderState(folderId);
        return Task.CompletedTask;
    }

    public Task RetryFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => RefreshFolderAsync(folderId, cancellationToken);

    public Task EnqueueMissingAsync(CancellationToken cancellationToken = default)
    {
        _state.EnsureInitialized();
        _logger.LogInformation("Metadata enqueue missing requested.");
        var plan = _planner.BuildEnqueueMissingPlan(_state.GetAllRecords());
        foreach (var item in plan)
        {
            _state.SaveRecord(item.Record);
        }
        EnqueuePlans(plan.Select(item => item.TaskPlan).OfType<MetadataTaskPlan>());
        _state.PublishSummaryChanged();
        return Task.CompletedTask;
    }

    public Task RetryFailedAsync(bool includeNoMatch, CancellationToken cancellationToken = default)
    {
        _state.EnsureInitialized();
        _logger.LogInformation("Metadata retry failed requested. IncludeNoMatch={IncludeNoMatch}", includeNoMatch);
        var plan = _planner.BuildRetryFailedPlan(_state.GetAllRecords(), includeNoMatch);
        foreach (var item in plan)
        {
            _state.SaveRecord(item.Record);
        }
        EnqueuePlans(plan.Select(item => item.TaskPlan).OfType<MetadataTaskPlan>());
        _state.PublishSummaryChanged();
        return Task.CompletedTask;
    }

    public async Task<MetadataProcessingResultDto> ProcessQueueAsync(int maxItems, CancellationToken cancellationToken = default)
    {
        _state.EnsureInitialized();
        _logger.LogInformation("Metadata manual process queue requested. MaxItems={MaxItems}", maxItems);
        var processed = new List<string>();
        var count = Math.Max(1, maxItems);

        for (var index = 0; index < count; index++)
        {
            var next = _state.GetAllRecords()
                .FirstOrDefault(item => item.State == MetadataState.Queued);
            if (next is null)
                break;

            cancellationToken.ThrowIfCancellationRequested();
            await _state.ExecuteAsync(next, cancellationToken);
            processed.Add(next.FolderId);
        }

        return new MetadataProcessingResultDto(processed.Count, processed);
    }

    public MetadataFolderStateSummary GetFolderStateSummary(string folderId)
    {
        _state.EnsureInitialized();
        return _state.GetFolderStateSummary(folderId);
    }

    private void EnqueuePlans(IEnumerable<MetadataTaskPlan> plans)
    {
        foreach (var plan in plans)
            EnqueuePlan(plan);
    }

    private void EnqueuePlan(MetadataTaskPlan plan)
    {
        _logger.LogInformation(
            "Metadata task plan created. FolderId={FolderId}, Reason={Reason}, Priority={Priority}, BypassCooldown={BypassCooldown}",
            plan.FolderId,
            plan.Reason,
            plan.Priority,
            plan.BypassCooldown);
        _queue.Enqueue(plan);
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
