using AniNest.Application.Metadata;
using AniNest.Application.Resources;
using AniNest.Contracts.Metadata;
using AniNest.Core.Enums;
using AniNest.Host.Events;
using Microsoft.Extensions.Logging;

namespace AniNest.Host.Modules;

internal sealed class MetadataRuntimeStateService : IMetadataRuntimeStateService
{
    private readonly IMetadataStore _legacyStore;
    private readonly IMetadataRecordStore _recordStore;
    private readonly IMetadataReviewStore _reviewStore;
    private readonly IMetadataAssetService _assets;
    private readonly IMetadataProjectionService _projection;
    private readonly IMetadataFetchPipeline _pipeline;
    private readonly IResourceUrlService _resourceUrlService;
    private readonly IHostEventStream _events;
    private readonly ILogger<MetadataRuntimeStateService> _logger;

    public MetadataRuntimeStateService(
        IMetadataStore legacyStore,
        IMetadataRecordStore recordStore,
        IMetadataReviewStore reviewStore,
        IMetadataAssetService assets,
        IMetadataProjectionService projection,
        IMetadataFetchPipeline pipeline,
        IResourceUrlService resourceUrlService,
        IHostEventStream events,
        ILogger<MetadataRuntimeStateService> logger)
    {
        _legacyStore = legacyStore;
        _recordStore = recordStore;
        _reviewStore = reviewStore;
        _assets = assets;
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
        _events.Publish("metadata.folder_updated", new
        {
            folderId,
            state = metadata?.State.ToString(),
            failureKind = metadata?.FailureKind.ToString(),
            hasMetadata = summary.HasMetadata,
            matchedTitle = metadata?.Title ?? summary.Title,
            originalTitle = metadata?.OriginalTitle,
            title = summary.Title,
            posterUrl = string.IsNullOrWhiteSpace(summary.PosterPath)
                ? null
                : _resourceUrlService.GetUrl(new ResourceKey(ResourceKind.LibraryPoster, folderId)),
            coverUrl = !summary.HasMetadata
                ? null
                : _resourceUrlService.GetUrl(new ResourceKey(ResourceKind.LibraryCover, folderId)),
            updatedAtUtc = DateTimeOffset.UtcNow
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

            var completedRecord = scraping with
            {
                State = MetadataState.Ready,
                FailureKind = MetadataFailureKind.None,
                SourceId = resolution.SourceId,
                MetadataFilePath = assets.PayloadPath,
                PosterFilePath = assets.PosterFilePath,
                LastSucceededAtUtc = DateTime.UtcNow
            };
            _recordStore.Save(completedRecord);
            _reviewStore.Delete(completedRecord.FolderId);
            _legacyStore.Save(new MetadataDto(
                completedRecord.FolderId,
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
            _assets.DeleteAssets(scraping.MetadataFilePath, scraping.PosterFilePath);

            if (resolution.ReviewRecord is not null)
                _reviewStore.Save(resolution.ReviewRecord);

            var reviewRecord = scraping with
            {
                State = resolution.NextState,
                FailureKind = resolution.FailureKind,
                SourceId = resolution.SourceId,
                MetadataFilePath = null,
                PosterFilePath = null
            };
            _recordStore.Save(reviewRecord);
            _legacyStore.Save(new MetadataDto(
                reviewRecord.FolderId,
                null,
                null,
                resolution.Reason,
                [],
                null,
                null,
                null,
                null,
                reviewRecord.State,
                reviewRecord.FailureKind));

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

        var readyRecord = record with
        {
            State = MetadataState.Ready,
            MetadataFilePath = assets.PayloadPath,
            PosterFilePath = assets.PosterFilePath,
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
            readyRecord.FailureKind,
            payload.AirDate,
            payload.Year,
            payload.Rating));

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
