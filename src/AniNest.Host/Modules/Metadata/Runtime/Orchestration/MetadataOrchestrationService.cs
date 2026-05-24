using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class MetadataOrchestrationService : IMetadataOrchestrationService
{
    private readonly IMetadataRuntimeBootstrapService _bootstrap;
    private readonly IMetadataRuntimeStateService _state;
    private readonly IMetadataTaskPlanner _planner;
    private readonly IMetadataTaskQueue _queue;
    private readonly ILogger<MetadataOrchestrationService> _logger;

    public MetadataOrchestrationService(
        IMetadataRuntimeBootstrapService bootstrap,
        IMetadataRuntimeStateService state,
        IMetadataTaskPlanner planner,
        IMetadataTaskQueue queue,
        ILogger<MetadataOrchestrationService> logger)
    {
        _bootstrap = bootstrap;
        _state = state;
        _planner = planner;
        _queue = queue;
        _logger = logger;
    }

    public Task SyncLibrarySnapshotAsync(IReadOnlyList<MetadataFolderRef> folders, CancellationToken cancellationToken = default)
    {
        _bootstrap.EnsureInitialized();
        _logger.LogInformation(
            "Metadata snapshot sync started. FolderCount={FolderCount}, RecordCount={RecordCount}",
            folders.Count,
            _state.GetAllRecords().Count);
        _bootstrap.NormalizeTransientStates();
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
            else if (record.State == AniNest.Core.Enums.MetadataState.NeedsMetadata)
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
        _bootstrap.EnsureInitialized();
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
        _bootstrap.EnsureInitialized();
        _logger.LogInformation("Metadata enqueue missing requested.");
        var plan = _planner.BuildEnqueueMissingPlan(_state.GetAllRecords());
        foreach (var item in plan)
            _state.SaveRecord(item.Record);

        EnqueuePlans(plan.Select(item => item.TaskPlan).OfType<MetadataTaskPlan>());
        _state.PublishSummaryChanged();
        return Task.CompletedTask;
    }

    public Task RetryFailedAsync(bool includeNoMatch, CancellationToken cancellationToken = default)
    {
        _bootstrap.EnsureInitialized();
        _logger.LogInformation("Metadata retry failed requested. IncludeNoMatch={IncludeNoMatch}", includeNoMatch);
        var plan = _planner.BuildRetryFailedPlan(_state.GetAllRecords(), includeNoMatch);
        foreach (var item in plan)
            _state.SaveRecord(item.Record);

        EnqueuePlans(plan.Select(item => item.TaskPlan).OfType<MetadataTaskPlan>());
        _state.PublishSummaryChanged();
        return Task.CompletedTask;
    }

    public async Task<MetadataProcessingResultDto> ProcessQueueAsync(int maxItems, CancellationToken cancellationToken = default)
    {
        _bootstrap.EnsureInitialized();
        _logger.LogInformation("Metadata manual process queue requested. MaxItems={MaxItems}", maxItems);
        var processed = new List<string>();
        var count = Math.Max(1, maxItems);

        for (var index = 0; index < count; index++)
        {
            var next = _state.GetAllRecords()
                .FirstOrDefault(item => item.State == AniNest.Core.Enums.MetadataState.Queued);
            if (next is null)
                break;

            cancellationToken.ThrowIfCancellationRequested();
            await _state.ExecuteAsync(next, cancellationToken);
            processed.Add(next.FolderId);
        }

        return new MetadataProcessingResultDto(processed.Count, processed);
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
}
