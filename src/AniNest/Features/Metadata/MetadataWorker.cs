using AniNest.Infrastructure.Logging;

namespace AniNest.Features.Metadata;

public sealed class MetadataWorker : IDisposable
{
    private static readonly Logger Log = AppLog.For<MetadataWorker>();
    private readonly MetadataTaskStore _taskStore;
    private readonly MetadataIndexStore _indexStore;
    private readonly IMetadataRepository _metadataRepository;
    private readonly IMetadataImageCache _imageCache;
    private readonly IMetadataProvider _metadataProvider;
    private readonly MetadataEventHub _events;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly object _stateSync = new();
    private Task? _workerTask;
    private bool _started;

    public MetadataWorker(
        MetadataTaskStore taskStore,
        MetadataIndexStore indexStore,
        IMetadataRepository metadataRepository,
        IMetadataImageCache imageCache,
        IMetadataProvider metadataProvider,
        MetadataEventHub events)
    {
        _taskStore = taskStore;
        _indexStore = indexStore;
        _metadataRepository = metadataRepository;
        _imageCache = imageCache;
        _metadataProvider = metadataProvider;
        _events = events;
    }

    public void Start()
    {
        lock (_stateSync)
        {
            if (_started)
                return;

            _started = true;
            Log.Info($"Metadata worker start requested: worker={GetHashCode()} taskStore={_taskStore.GetHashCode()}");
            _workerTask = Task.Run(() => RunAsync(_shutdownCts.Token));
        }
    }

    public void Dispose()
    {
        _shutdownCts.Cancel();

        try
        {
            _workerTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
        }

        _shutdownCts.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        Log.Info($"Metadata worker started: worker={GetHashCode()} taskStore={_taskStore.GetHashCode()}");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                string folderPath = await _taskStore.DequeueAsync(ct);
                await ProcessFolderAsync(folderPath, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Log.Error("Metadata worker loop failed", ex);
        }
        finally
        {
            Log.Info("Metadata worker stopped");
        }
    }

    private async Task ProcessFolderAsync(string folderPath, CancellationToken ct)
    {
        MetadataRecord? record;
        var records = _indexStore.Load();
        if (!records.TryGetValue(folderPath, out record))
            return;

        if (record.State != MetadataState.Queued)
            return;

        record.State = MetadataState.Scraping;
        record.LastAttemptAtUtc = DateTime.UtcNow;
        _indexStore.Save(records);
        PublishSummary(records);

        var folder = new MetadataFolderRef(record.FolderPath, record.FolderName, Array.Empty<string>());
        MetadataFetchResult result = await _metadataProvider.FetchAsync(folder, ct);

        records = _indexStore.Load();
        if (!records.TryGetValue(folderPath, out record))
            return;

        switch (result.Outcome)
        {
            case MetadataFetchOutcome.Success:
                await HandleSuccessAsync(record, result.Metadata!, records, ct);
                break;
            case MetadataFetchOutcome.NoMatch:
                HandleFailure(record, records, MetadataState.NeedsReview, MetadataFailureKind.NoMatch, TimeSpan.FromDays(7));
                break;
            case MetadataFetchOutcome.NetworkError:
                HandleFailure(record, records, MetadataState.NeedsMetadata, MetadataFailureKind.NetworkError, TimeSpan.FromMinutes(30));
                break;
            default:
                HandleFailure(record, records, MetadataState.NeedsMetadata, MetadataFailureKind.ProviderError, TimeSpan.FromDays(1));
                break;
        }
    }

    private async Task HandleSuccessAsync(
        MetadataRecord record,
        FolderMetadata metadata,
        Dictionary<string, MetadataRecord> records,
        CancellationToken ct)
    {
        string? posterPath = null;
        if (!string.IsNullOrWhiteSpace(metadata.PosterUrl))
            posterPath = await _imageCache.CachePosterAsync(record.FolderPath, metadata.PosterUrl, ct);

        metadata.LocalPosterPath = posterPath;
        _metadataRepository.Save(metadata);

        record.State = MetadataState.Ready;
        record.FailureKind = MetadataFailureKind.None;
        record.SourceId = metadata.SourceId;
        record.LastSucceededAtUtc = metadata.ScrapedAt;
        record.CooldownUntilUtc = null;
        record.MetadataFilePath = MetadataStoragePaths.GetMetadataFilePath(record.FolderPath);
        record.PosterFilePath = posterPath;

        _indexStore.Save(records);
        PublishSummary(records);
        _events.RaiseFolderMetadataRefreshed(record.FolderPath, metadata);
    }

    private void HandleFailure(
        MetadataRecord record,
        Dictionary<string, MetadataRecord> records,
        MetadataState state,
        MetadataFailureKind failureKind,
        TimeSpan cooldown)
    {
        record.State = state;
        record.FailureKind = failureKind;
        record.CooldownUntilUtc = DateTime.UtcNow.Add(cooldown);
        _indexStore.Save(records);
        PublishSummary(records);
    }

    private void PublishSummary(IReadOnlyDictionary<string, MetadataRecord> records)
    {
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
