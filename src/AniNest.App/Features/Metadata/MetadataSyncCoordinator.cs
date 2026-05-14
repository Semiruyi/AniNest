using System.IO;
using System.Security.Cryptography;
using System.Text;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Persistence;

namespace AniNest.Features.Metadata;

public sealed class MetadataSyncCoordinator : IMetadataSyncService
{
    private static readonly Logger Log = AppLog.For<MetadataSyncCoordinator>();
    private static readonly MetadataStatusSummary EmptySummary = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    private readonly MetadataIndexStore _indexStore;
    private readonly IMetadataRepository _metadataRepository;
    private readonly ISettingsService _settings;
    private readonly MetadataTaskStore _taskStore;
    private readonly MetadataEventHub _events;

    public MetadataSyncCoordinator(
        MetadataIndexStore indexStore,
        IMetadataRepository metadataRepository,
        ISettingsService settings,
        MetadataTaskStore taskStore,
        MetadataEventHub events)
    {
        _indexStore = indexStore;
        _metadataRepository = metadataRepository;
        _settings = settings;
        _taskStore = taskStore;
        _events = events;
    }

    public Task SyncLibrarySnapshotAsync(
        IReadOnlyList<MetadataFolderRef> folders,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Log.Info($"SyncLibrarySnapshot start: folders={folders.Count}");

        bool autoEnabled = _settings.Load().AutoScrapeMetadata;
        var records = _indexStore.Load();
        var currentPaths = new HashSet<string>(
            folders.Select(folder => folder.FolderPath),
            StringComparer.OrdinalIgnoreCase);

        NormalizeRuntimeStates(records, autoEnabled);

        foreach (var orphanPath in records.Keys.Where(path => !currentPaths.Contains(path)).ToArray())
        {
            CleanupRecord(records[orphanPath]);
            records.Remove(orphanPath);
        }

        foreach (var folder in folders)
        {
            ct.ThrowIfCancellationRequested();
            UpsertRecord(records, folder, autoEnabled);
        }

        var queuedPaths = QueueEligibleRecords(records, autoEnabled);
        _indexStore.Save(records);
        PublishQueuedPaths(queuedPaths);
        Log.Info($"SyncLibrarySnapshot saved: records={records.Count}");
        PublishSummary(records);
        return Task.CompletedTask;
    }

    public Task RegisterFolderAsync(MetadataFolderRef folder, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        bool autoEnabled = _settings.Load().AutoScrapeMetadata;
        var records = _indexStore.Load();
        NormalizeRuntimeStates(records, autoEnabled);
        UpsertRecord(records, folder, autoEnabled);
        var queuedPaths = QueueEligibleRecords(records, autoEnabled, folder.FolderPath);
        _indexStore.Save(records);
        PublishQueuedPaths(queuedPaths);
        PublishSummary(records);
        return Task.CompletedTask;
    }

    public Task DeleteFolderAsync(string folderPath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var records = _indexStore.Load();
        if (records.TryGetValue(folderPath, out var record))
        {
            CleanupRecord(record);
            records.Remove(folderPath);
            _indexStore.Save(records);
            PublishSummary(records);
        }

        return Task.CompletedTask;
    }

    public Task EnqueueMissingAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var records = _indexStore.Load();
        var queuedPaths = QueueEligibleRecords(records, autoEnabled: true);
        _indexStore.Save(records);
        PublishQueuedPaths(queuedPaths);
        PublishSummary(records);
        return Task.CompletedTask;
    }

    public Task RetryFailedAsync(bool includeNoMatch = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var records = _indexStore.Load();
        foreach (var record in records.Values)
        {
            if (record.FailureKind == MetadataFailureKind.None)
                continue;

            if (record.FailureKind == MetadataFailureKind.NoMatch && !includeNoMatch)
                continue;

            record.State = MetadataState.NeedsMetadata;
            record.FailureKind = MetadataFailureKind.None;
            record.CooldownUntilUtc = null;
        }

        var queuedPaths = QueueEligibleRecords(records, autoEnabled: true);
        _indexStore.Save(records);
        PublishQueuedPaths(queuedPaths);
        PublishSummary(records);
        return Task.CompletedTask;
    }

    public Task SetAutoMetadataEnabledAsync(bool enabled, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var settings = _settings.Load();
        if (settings.AutoScrapeMetadata == enabled)
            return Task.CompletedTask;

        settings.AutoScrapeMetadata = enabled;
        _settings.Save();

        var records = _indexStore.Load();
        NormalizeRuntimeStates(records, enabled);
        foreach (var record in records.Values)
        {
            if (!enabled && record.State != MetadataState.Ready)
                record.State = MetadataState.Disabled;
            else if (enabled && record.State == MetadataState.Disabled)
                record.State = MetadataState.NeedsMetadata;
        }

        var queuedPaths = QueueEligibleRecords(records, enabled);
        _indexStore.Save(records);
        PublishQueuedPaths(queuedPaths);
        PublishSummary(records);
        return Task.CompletedTask;
    }

    private static void NormalizeRuntimeStates(
        IDictionary<string, MetadataRecord> records,
        bool autoEnabled)
    {
        foreach (var record in records.Values)
        {
            if (record.State is MetadataState.Queued or MetadataState.Scraping)
                record.State = autoEnabled ? MetadataState.NeedsMetadata : MetadataState.Disabled;
        }
    }

    private static string ComputeFingerprint(MetadataFolderRef folder)
    {
        var builder = new StringBuilder();
        builder.Append(folder.FolderName.Trim().ToLowerInvariant());
        builder.Append('|').Append(folder.VideoFiles.Count);

        foreach (var fileName in folder.VideoFiles
                     .Select(Path.GetFileName)
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                     .Take(5))
        {
            builder.Append('|').Append(fileName!.ToLowerInvariant());
        }

        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant()[..12];
    }

    private void UpsertRecord(
        IDictionary<string, MetadataRecord> records,
        MetadataFolderRef folder,
        bool autoEnabled)
    {
        string fingerprint = ComputeFingerprint(folder);

        if (!records.TryGetValue(folder.FolderPath, out var record))
        {
            records[folder.FolderPath] = new MetadataRecord
            {
                FolderPath = folder.FolderPath,
                FolderName = folder.FolderName,
                FolderFingerprint = fingerprint,
                State = autoEnabled ? MetadataState.NeedsMetadata : MetadataState.Disabled,
                FailureKind = MetadataFailureKind.None
            };
            return;
        }

        bool fingerprintChanged = !string.Equals(
            record.FolderFingerprint,
            fingerprint,
            StringComparison.Ordinal);

        if (fingerprintChanged)
            ResetRecordForRefresh(record, folder, fingerprint, autoEnabled);
        else
            record.FolderName = folder.FolderName;

        if (!fingerprintChanged)
        {
            if (!autoEnabled && record.State != MetadataState.Ready)
            {
                record.State = MetadataState.Disabled;
            }
            else if (autoEnabled && record.State == MetadataState.Disabled)
            {
                record.State = MetadataState.NeedsMetadata;
            }
        }
    }

    private void ResetRecordForRefresh(
        MetadataRecord record,
        MetadataFolderRef folder,
        string fingerprint,
        bool autoEnabled)
    {
        CleanupRecord(record);
        record.FolderName = folder.FolderName;
        record.FolderFingerprint = fingerprint;
        record.State = autoEnabled ? MetadataState.NeedsMetadata : MetadataState.Disabled;
        record.FailureKind = MetadataFailureKind.None;
        record.SourceId = null;
        record.LastAttemptAtUtc = null;
        record.LastSucceededAtUtc = null;
        record.CooldownUntilUtc = null;
        record.MetadataFilePath = null;
        record.PosterFilePath = null;
    }

    private void CleanupRecord(MetadataRecord record)
    {
        _metadataRepository.Delete(record.FolderPath);

        if (!string.IsNullOrWhiteSpace(record.PosterFilePath) && File.Exists(record.PosterFilePath))
            File.Delete(record.PosterFilePath);

        if (!string.IsNullOrWhiteSpace(record.MetadataFilePath) && File.Exists(record.MetadataFilePath))
            File.Delete(record.MetadataFilePath);
    }

    private List<string> QueueEligibleRecords(
        IDictionary<string, MetadataRecord> records,
        bool autoEnabled,
        string? preferredPath = null)
    {
        var queuedPaths = new List<string>();
        if (!autoEnabled)
            return queuedPaths;

        if (!string.IsNullOrWhiteSpace(preferredPath) &&
            records.TryGetValue(preferredPath, out var preferredRecord))
        {
            TryQueueRecord(preferredRecord, queuedPaths);
        }

        foreach (var record in records.Values)
        {
            if (string.Equals(record.FolderPath, preferredPath, StringComparison.OrdinalIgnoreCase))
                continue;

            TryQueueRecord(record, queuedPaths);
        }

        return queuedPaths;
    }

    private void TryQueueRecord(MetadataRecord record, ICollection<string> queuedPaths)
    {
        if (record.State != MetadataState.NeedsMetadata)
            return;

        if (record.CooldownUntilUtc.HasValue && record.CooldownUntilUtc.Value > DateTime.UtcNow)
            return;

        Log.Info($"Record marked queued: taskStore={_taskStore.GetHashCode()} path={record.FolderPath}");
        record.State = MetadataState.Queued;
        queuedPaths.Add(record.FolderPath);
    }

    private void PublishQueuedPaths(IReadOnlyList<string> queuedPaths)
    {
        foreach (var path in queuedPaths)
        {
            if (_taskStore.Enqueue(path))
            {
                Log.Info($"Record enqueued: taskStore={_taskStore.GetHashCode()} path={path}");
            }
        }
    }

    private void PublishSummary(IReadOnlyDictionary<string, MetadataRecord> records)
    {
        if (records.Count == 0)
        {
            _events.RaiseSummaryChanged(EmptySummary);
            return;
        }

        var values = records.Values;
        _events.RaiseSummaryChanged(new MetadataStatusSummary(
            values.Count(record => record.State == MetadataState.NeedsMetadata),
            values.Count(record => record.State == MetadataState.Queued),
            values.Count(record => record.State == MetadataState.Scraping),
            values.Count(record => record.State == MetadataState.Ready),
            values.Count(record => record.State == MetadataState.NeedsReview),
            values.Count(record => record.State == MetadataState.Disabled),
            values.Count(record => record.FailureKind == MetadataFailureKind.NetworkError),
            values.Count(record => record.FailureKind == MetadataFailureKind.NoMatch),
            values.Count(record => record.FailureKind == MetadataFailureKind.ProviderError)));
    }
}
