using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;
using AniNest.Core.Enums;
using AniNest.Host.Events;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class MetadataRuntimeStateService : IMetadataRuntimeStateService
{
    private readonly IMetadataStore _legacyStore;
    private readonly IMetadataRecordStore _recordStore;
    private readonly IMetadataPayloadRepository _payloadRepository;
    private readonly IHostEventStream _events;
    private readonly ILogger<MetadataRuntimeStateService> _logger;

    public MetadataRuntimeStateService(
        IMetadataStore legacyStore,
        IMetadataRecordStore recordStore,
        IMetadataPayloadRepository payloadRepository,
        IHostEventStream events,
        ILogger<MetadataRuntimeStateService> logger)
    {
        _legacyStore = legacyStore;
        _recordStore = recordStore;
        _payloadRepository = payloadRepository;
        _events = events;
        _logger = logger;
    }

    public void EnsureInitialized()
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
                : MetadataStoragePathCodec.GetPayloadPath(metadata.FolderId);

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

    public IReadOnlyList<MetadataRecord> GetAllRecords() => _recordStore.GetAll();

    public MetadataRecord? GetRecord(string folderId) => _recordStore.GetByFolderId(folderId);

    public MetadataRecord RequireRecord(string folderId)
        => _recordStore.GetByFolderId(folderId)
           ?? throw new KeyNotFoundException($"Metadata for folder '{folderId}' was not found.");

    public void SaveRecord(MetadataRecord record) => _recordStore.Save(record);

    public void DeleteRecord(string folderId)
    {
        var record = _recordStore.GetByFolderId(folderId);
        if (!string.IsNullOrWhiteSpace(record?.MetadataFilePath))
            _payloadRepository.Delete(record.MetadataFilePath);

        _recordStore.Delete(folderId);
    }

    public MetadataDto? GetMetadata(string folderId)
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

    public MetadataStatusSummaryDto BuildSummary()
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

    public MetadataFolderStateSummary GetFolderStateSummary(string folderId)
    {
        var metadata = GetMetadata(folderId);
        return metadata is null
            ? new MetadataFolderStateSummary(false, MetadataState.NeedsMetadata, null, null)
            : new MetadataFolderStateSummary(
                !string.IsNullOrWhiteSpace(metadata.Title) || !string.IsNullOrWhiteSpace(metadata.PosterPath),
                metadata.State,
                metadata.Title,
                metadata.PosterPath);
    }

    public void PublishFolderState(string folderId)
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

    public void PublishSummaryChanged()
    {
        var summary = BuildSummary();
        _logger.LogInformation(
            "Metadata summary changed. NeedsMetadata={NeedsMetadata}, Queued={Queued}, Scraping={Scraping}, Ready={Ready}, NeedsReview={NeedsReview}",
            summary.NeedsMetadata,
            summary.Queued,
            summary.Scraping,
            summary.Ready,
            summary.NeedsReview);
        _events.Publish("metadata.summary_changed", summary);
    }

    public void NormalizeTransientStates()
    {
        foreach (var record in _recordStore.GetAll())
        {
            if (record.State is not (MetadataState.Queued or MetadataState.Scraping))
                continue;

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

    public void ExecutePlaceholder(MetadataRecord record)
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
        var payloadPath = MetadataStoragePathCodec.GetPayloadPath(record.FolderId);
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

        if (!string.IsNullOrWhiteSpace(record.MetadataFilePath) &&
            !string.Equals(record.MetadataFilePath, payloadPath, StringComparison.OrdinalIgnoreCase))
        {
            _payloadRepository.Delete(record.MetadataFilePath);
        }

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
