using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;
using AniNest.Core.Enums;
using AniNest.Host.Events;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class MetadataLifecycleService : IMetadataLifecycleService
{
    private readonly IMetadataStore _legacyStore;
    private readonly IMetadataRecordStore _recordStore;
    private readonly IMetadataPayloadRepository _payloadRepository;
    private readonly IMetadataTaskScheduler _scheduler;
    private readonly IHostEventStream _events;
    private readonly ILogger<MetadataLifecycleService> _logger;

    public MetadataLifecycleService(
        IMetadataStore legacyStore,
        IMetadataRecordStore recordStore,
        IMetadataPayloadRepository payloadRepository,
        IMetadataTaskScheduler scheduler,
        IHostEventStream events,
        ILogger<MetadataLifecycleService> logger)
    {
        _legacyStore = legacyStore;
        _recordStore = recordStore;
        _payloadRepository = payloadRepository;
        _scheduler = scheduler;
        _events = events;
        _logger = logger;
    }

    public Task<MetadataDto?> GetByFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        EnsureLegacyRecordsLoaded();
        return Task.FromResult(GetMetadata(folderId));
    }

    public Task<MetadataStatusSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        EnsureLegacyRecordsLoaded();
        return Task.FromResult(BuildSummary());
    }

    public Task SyncLibrarySnapshotAsync(IReadOnlyList<MetadataFolderRef> folders, CancellationToken cancellationToken = default)
    {
        EnsureLegacyRecordsLoaded();
        _logger.LogInformation(
            "Metadata snapshot sync started. FolderCount={FolderCount}, RecordCount={RecordCount}",
            folders.Count,
            _recordStore.GetAll().Count);
        var records = _recordStore.GetAll().ToDictionary(item => item.FolderId, StringComparer.OrdinalIgnoreCase);
        var knownFolderIds = folders.Select(folder => folder.FolderId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var record in records.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (record.State is MetadataState.Queued or MetadataState.Scraping)
            {
                _logger.LogInformation(
                    "Metadata stale runtime state normalized. FolderId={FolderId}, PreviousState={PreviousState}",
                    record.FolderId,
                    record.State);
                _recordStore.Save(record with
                {
                    State = MetadataState.NeedsMetadata,
                    FailureKind = MetadataFailureKind.None
                });
            }
        }

        foreach (var record in records.Values.Where(record => !knownFolderIds.Contains(record.FolderId)))
        {
            _logger.LogInformation(
                "Metadata record removed because folder is no longer in library snapshot. FolderId={FolderId}",
                record.FolderId);
            _recordStore.Delete(record.FolderId);
        }

        foreach (var folder in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!records.TryGetValue(folder.FolderId, out var existing))
            {
                var created = MetadataStorageDefaults.CreatePlaceholder(folder.FolderId, folder.FolderPath, folder.FolderName);
                _recordStore.Save(created);
                records[folder.FolderId] = created;
                _logger.LogInformation(
                    "Metadata placeholder record created. FolderId={FolderId}, FolderName={FolderName}",
                    folder.FolderId,
                    folder.FolderName);
                EnqueuePlan(folder.FolderId, MetadataTaskReason.LibraryReconcile, priority: 20, bypassCooldown: false);
                continue;
            }

            var normalized = existing with
            {
                FolderPath = folder.FolderPath,
                FolderName = folder.FolderName
            };
            _recordStore.Save(normalized);

            if (normalized.State == MetadataState.NeedsMetadata)
            {
                _logger.LogInformation(
                    "Metadata folder eligible after snapshot sync. FolderId={FolderId}",
                    folder.FolderId);
                EnqueuePlan(folder.FolderId, MetadataTaskReason.LibraryReconcile, priority: 20, bypassCooldown: false);
            }
        }

        _logger.LogInformation("Metadata snapshot sync completed.");
        PublishSummaryChanged();
        return Task.CompletedTask;
    }

    public Task RefreshFolderAsync(string folderId, CancellationToken cancellationToken = default)
    {
        EnsureLegacyRecordsLoaded();
        _logger.LogInformation("Metadata refresh requested. FolderId={FolderId}", folderId);
        var current = RequireRecord(folderId);
        _recordStore.Save(current with
        {
            State = MetadataState.Queued,
            FailureKind = MetadataFailureKind.None,
            LastAttemptAtUtc = DateTime.UtcNow
        });
        EnqueuePlan(folderId, MetadataTaskReason.ManualRefresh, priority: 0, bypassCooldown: true);
        PublishFolderState(folderId);
        return Task.CompletedTask;
    }

    public Task RetryFolderAsync(string folderId, CancellationToken cancellationToken = default)
        => RefreshFolderAsync(folderId, cancellationToken);

    public Task EnqueueMissingAsync(CancellationToken cancellationToken = default)
    {
        EnsureLegacyRecordsLoaded();
        _logger.LogInformation("Metadata enqueue missing requested.");
        foreach (var record in _recordStore.GetAll().Where(item => item.State == MetadataState.NeedsMetadata))
        {
            _recordStore.Save(record with { State = MetadataState.Queued });
            EnqueuePlan(record.FolderId, MetadataTaskReason.MissingMetadata, priority: 30, bypassCooldown: false);
        }

        PublishSummaryChanged();
        return Task.CompletedTask;
    }

    public Task RetryFailedAsync(bool includeNoMatch, CancellationToken cancellationToken = default)
    {
        EnsureLegacyRecordsLoaded();
        _logger.LogInformation("Metadata retry failed requested. IncludeNoMatch={IncludeNoMatch}", includeNoMatch);
        foreach (var record in _recordStore.GetAll())
        {
            if (record.FailureKind == MetadataFailureKind.None)
                continue;
            if (record.FailureKind == MetadataFailureKind.NoMatch && !includeNoMatch)
                continue;

            _recordStore.Save(record with
            {
                State = MetadataState.Queued,
                FailureKind = MetadataFailureKind.None,
                CooldownUntilUtc = null
            });
            EnqueuePlan(record.FolderId, MetadataTaskReason.RetryFailed, priority: 40, bypassCooldown: true);
        }

        PublishSummaryChanged();
        return Task.CompletedTask;
    }

    public Task<MetadataProcessingResultDto> ProcessQueueAsync(int maxItems, CancellationToken cancellationToken = default)
    {
        EnsureLegacyRecordsLoaded();
        _logger.LogInformation("Metadata manual process queue requested. MaxItems={MaxItems}", maxItems);
        var processed = new List<string>();
        var count = Math.Max(1, maxItems);

        for (var index = 0; index < count; index++)
        {
            var next = _recordStore.GetAll()
                .FirstOrDefault(item => item.State == MetadataState.Queued);
            if (next is null)
                break;

            cancellationToken.ThrowIfCancellationRequested();
            ExecutePlaceholder(next);
            processed.Add(next.FolderId);
        }

        return Task.FromResult(new MetadataProcessingResultDto(processed.Count, processed));
    }

    public MetadataFolderStateSummary GetFolderStateSummary(string folderId)
    {
        EnsureLegacyRecordsLoaded();
        var metadata = GetMetadata(folderId);
        return metadata is null
            ? new MetadataFolderStateSummary(false, MetadataState.NeedsMetadata, null, null)
            : new MetadataFolderStateSummary(
                !string.IsNullOrWhiteSpace(metadata.Title) || !string.IsNullOrWhiteSpace(metadata.PosterPath),
                metadata.State,
                metadata.Title,
                metadata.PosterPath);
    }

    internal void ExecutePlaceholder(MetadataRecord record)
    {
        _logger.LogInformation(
            "Metadata placeholder execution started. FolderId={FolderId}, State={State}",
            record.FolderId,
            record.State);
        var scraping = record with
        {
            State = MetadataState.Scraping,
            FailureKind = MetadataFailureKind.None,
            LastAttemptAtUtc = DateTime.UtcNow
        };
        _recordStore.Save(scraping);
        PublishFolderState(record.FolderId);

        var title = string.IsNullOrWhiteSpace(record.FolderName)
            ? FormatFolderTitle(record.FolderId)
            : record.FolderName;
        var payloadPath = GetPayloadPath(record.FolderId);
        var payload = new FolderMetadataPayload(
            record.FolderId,
            record.SourceId,
            title,
            title,
            $"Metadata lifecycle placeholder generated for {title}.",
            null,
            null,
            null,
            null,
            null,
            null,
            ["placeholder"],
            "placeholder",
            DateTime.UtcNow);
        _payloadRepository.Save(payloadPath, payload);

        var readyRecord = scraping with
        {
            State = MetadataState.Ready,
            MetadataFilePath = payloadPath,
            LastSucceededAtUtc = DateTime.UtcNow
        };
        _recordStore.Save(readyRecord);
        _legacyStore.Save(new MetadataDto(
            readyRecord.FolderId,
            payload.Title,
            payload.OriginalTitle,
            payload.Summary,
            payload.Tags,
            payload.LocalPosterPath,
            null,
            payload.EpisodeCount,
            payload.Source,
            readyRecord.State,
            readyRecord.FailureKind));

        _logger.LogInformation(
            "Metadata placeholder execution completed. FolderId={FolderId}, PayloadPath={PayloadPath}",
            record.FolderId,
            payloadPath);
        PublishFolderState(record.FolderId);
    }

    private void EnqueuePlan(string folderId, MetadataTaskReason reason, int priority, bool bypassCooldown)
    {
        _logger.LogInformation(
            "Metadata task plan created. FolderId={FolderId}, Reason={Reason}, Priority={Priority}, BypassCooldown={BypassCooldown}",
            folderId,
            reason,
            priority,
            bypassCooldown);
        _scheduler.Enqueue(new MetadataTaskPlan(folderId, reason, priority, bypassCooldown));
    }

    private void EnsureLegacyRecordsLoaded()
    {
        if (_recordStore.GetAll().Count > 0)
            return;

        _logger.LogInformation("Metadata record store empty. Importing legacy metadata records.");
        foreach (var metadata in _legacyStore.GetAll())
        {
            var payloadPath = string.IsNullOrWhiteSpace(metadata.Title) &&
                              string.IsNullOrWhiteSpace(metadata.OriginalTitle) &&
                              string.IsNullOrWhiteSpace(metadata.Summary) &&
                              metadata.Tags.Count == 0 &&
                              string.IsNullOrWhiteSpace(metadata.Source)
                ? null
                : GetPayloadPath(metadata.FolderId);

            if (!string.IsNullOrWhiteSpace(payloadPath))
            {
                _payloadRepository.Save(payloadPath, new FolderMetadataPayload(
                    metadata.FolderId,
                    null,
                    metadata.Title,
                    metadata.OriginalTitle,
                    metadata.Summary,
                    null,
                    metadata.PosterPath,
                    null,
                    null,
                    null,
                    metadata.EpisodeCount,
                    metadata.Tags,
                    metadata.Source,
                    DateTime.UtcNow));
            }

            _recordStore.Save(new MetadataRecord(
                metadata.FolderId,
                metadata.FolderId,
                metadata.Title ?? metadata.FolderId,
                string.Empty,
                metadata.State,
                metadata.FailureKind,
                null,
                null,
                metadata.State == MetadataState.Ready ? DateTime.UtcNow : null,
                null,
                payloadPath,
                metadata.PosterPath));

            _logger.LogInformation(
                "Metadata legacy record imported. FolderId={FolderId}, State={State}",
                metadata.FolderId,
                metadata.State);
        }
    }

    private MetadataRecord RequireRecord(string folderId)
        => _recordStore.GetByFolderId(folderId)
           ?? throw new KeyNotFoundException($"Metadata for folder '{folderId}' was not found.");

    private MetadataDto? GetMetadata(string folderId)
    {
        var record = _recordStore.GetByFolderId(folderId);
        if (record is null)
            return _legacyStore.GetByFolderId(folderId);

        FolderMetadataPayload? payload = null;
        if (!string.IsNullOrWhiteSpace(record.MetadataFilePath))
            payload = _payloadRepository.Load(record.MetadataFilePath);

        return new MetadataDto(
            record.FolderId,
            payload?.Title,
            payload?.OriginalTitle,
            payload?.Summary,
            payload?.Tags ?? [],
            payload?.LocalPosterPath,
            null,
            payload?.EpisodeCount,
            payload?.Source,
            record.State,
            record.FailureKind);
    }

    private MetadataStatusSummaryDto BuildSummary()
    {
        var all = _recordStore.GetAll();
        return new MetadataStatusSummaryDto(
            all.Count(item => item.State == MetadataState.NeedsMetadata),
            all.Count(item => item.State == MetadataState.Queued),
            all.Count(item => item.State == MetadataState.Scraping),
            all.Count(item => item.State == MetadataState.Ready),
            all.Count(item => item.State == MetadataState.NeedsReview),
            all.Count(item => item.State == MetadataState.Disabled),
            all.Count(item => item.FailureKind == MetadataFailureKind.NetworkError),
            all.Count(item => item.FailureKind == MetadataFailureKind.NoMatch),
            all.Count(item => item.FailureKind == MetadataFailureKind.ProviderError));
    }

    private void PublishFolderState(string folderId)
    {
        var metadata = GetMetadata(folderId);
        _events.Publish("metadata.folder_updated", new
        {
            folderId,
            state = metadata?.State.ToString(),
            failureKind = metadata?.FailureKind.ToString()
        });
        PublishSummaryChanged();
    }

    private void PublishSummaryChanged()
    {
        var summary = BuildSummary();
        _logger.LogInformation("Metadata summary changed. NeedsMetadata={NeedsMetadata}, Queued={Queued}, Scraping={Scraping}, Ready={Ready}, NeedsReview={NeedsReview}",
            summary.NeedsMetadata,
            summary.Queued,
            summary.Scraping,
            summary.Ready,
            summary.NeedsReview);
        _events.Publish("metadata.summary_changed", summary);
    }

    private static string GetPayloadPath(string folderId)
        => $"{folderId}.json";

    private static string FormatFolderTitle(string folderId)
    {
        if (string.IsNullOrWhiteSpace(folderId))
            return folderId;

        return string.Join(
            ' ',
            folderId
                .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..]));
    }
}
