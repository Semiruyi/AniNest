using AniNest.Application.Metadata;
using AniNest.Application.Resources;
using AniNest.Contracts.Metadata;
using AniNest.Core.Enums;
using AniNest.Host.Events;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class MetadataRuntimeStateService : IMetadataRuntimeStateService
{
    private readonly IMetadataRecordStore _recordStore;
    private readonly IMetadataReviewStore _reviewStore;
    private readonly IMetadataAssetService _assets;
    private readonly IMetadataLegacySyncService _legacySync;
    private readonly IMetadataReadyStateService _readyState;
    private readonly IMetadataPendingStateService _pendingState;
    private readonly IMetadataProjectionService _projection;
    private readonly IMetadataFetchPipeline _pipeline;
    private readonly IResourceUrlService _resourceUrlService;
    private readonly IHostEventStream _events;
    private readonly ILogger<MetadataRuntimeStateService> _logger;

    public MetadataRuntimeStateService(
        IMetadataRecordStore recordStore,
        IMetadataReviewStore reviewStore,
        IMetadataAssetService assets,
        IMetadataLegacySyncService legacySync,
        IMetadataReadyStateService readyState,
        IMetadataPendingStateService pendingState,
        IMetadataProjectionService projection,
        IMetadataFetchPipeline pipeline,
        IResourceUrlService resourceUrlService,
        IHostEventStream events,
        ILogger<MetadataRuntimeStateService> logger)
    {
        _recordStore = recordStore;
        _reviewStore = reviewStore;
        _assets = assets;
        _legacySync = legacySync;
        _readyState = readyState;
        _pendingState = pendingState;
        _projection = projection;
        _pipeline = pipeline;
        _resourceUrlService = resourceUrlService;
        _events = events;
        _logger = logger;
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
        _assets.DeleteAssets(record);
        _reviewStore.Delete(folderId);
        _recordStore.Delete(folderId);
    }

    public MetadataDto? GetMetadata(string folderId)
        => _projection.GetMetadata(folderId, _recordStore.GetByFolderId(folderId));

    public MetadataStatusSummaryDto BuildSummary()
        => _projection.BuildSummary(_recordStore.GetAll());

    public MetadataFolderStateSummary GetFolderStateSummary(string folderId)
        => _projection.BuildFolderStateSummary(GetMetadata(folderId));

    public void PublishFolderState(string folderId)
    {
        var metadata = GetMetadata(folderId);
        var summary = GetFolderStateSummary(folderId);
        _events.Publish(
            "metadata.folder_updated",
            MetadataEventPayloadMapper.BuildFolderUpdated(
                folderId,
                metadata,
                summary,
                _resourceUrlService));
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

    public async Task ExecuteAsync(MetadataRecord record, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Metadata execution started. FolderId={FolderId}, State={State}",
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

        try
        {
            var resolution = await _pipeline.ExecuteAsync(scraping, cancellationToken);
            if (await TryApplyResolutionAsync(scraping, resolution, cancellationToken))
                return;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Metadata pipeline failed and will fall back to placeholder. FolderId={FolderId}",
                record.FolderId);
        }

        ExecutePlaceholderFallback(scraping);
    }

    private async Task<bool> TryApplyResolutionAsync(
        MetadataRecord scraping,
        MetadataResolutionResult resolution,
        CancellationToken cancellationToken)
    {
        if (resolution.NextState == MetadataState.Ready && resolution.Payload is not null)
        {
            var assets = await _assets.SaveResolvedPayloadAsync(
                scraping.FolderId,
                resolution.Payload,
                scraping.MetadataFilePath,
                scraping.PosterFilePath,
                cancellationToken);

            var completedRecord = _readyState.SaveReady(scraping, resolution.SourceId, assets);
            _reviewStore.Delete(completedRecord.FolderId);

            _logger.LogInformation(
                "Metadata execution completed from pipeline. FolderId={FolderId}, PayloadPath={PayloadPath}, SourceId={SourceId}, PosterFilePath={PosterFilePath}",
                completedRecord.FolderId,
                assets.PayloadPath,
                completedRecord.SourceId,
                completedRecord.PosterFilePath ?? "(null)");
            PublishFolderState(completedRecord.FolderId);
            return true;
        }

        if (resolution.NextState is MetadataState.NeedsReview or MetadataState.NeedsMetadata)
        {
            var reviewRecord = _pendingState.SavePending(scraping, resolution);

            _logger.LogInformation(
                "Metadata execution completed with review state. FolderId={FolderId}, NextState={NextState}, FailureKind={FailureKind}, Reason={Reason}",
                reviewRecord.FolderId,
                reviewRecord.State,
                reviewRecord.FailureKind,
                resolution.Reason);
            PublishFolderState(reviewRecord.FolderId);
            return true;
        }

        return false;
    }

    private void ExecutePlaceholderFallback(MetadataRecord record)
    {
        _logger.LogInformation(
            "Metadata placeholder fallback started. FolderId={FolderId}, State={State}",
            record.FolderId,
            record.State);
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
        var assets = _assets.SavePlaceholderPayload(record, payload);

        var readyRecord = _readyState.SaveReady(record, record.SourceId, assets);

        _logger.LogInformation(
            "Metadata placeholder fallback completed. FolderId={FolderId}, PayloadPath={PayloadPath}",
            record.FolderId,
            assets.PayloadPath);
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
