using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal sealed class MetadataProjectionService : IMetadataProjectionService
{
    private readonly IMetadataStore _legacyStore;
    private readonly IMetadataPayloadRepository _payloadRepository;

    public MetadataProjectionService(
        IMetadataStore legacyStore,
        IMetadataPayloadRepository payloadRepository)
    {
        _legacyStore = legacyStore;
        _payloadRepository = payloadRepository;
    }

    public MetadataDto? GetMetadata(string folderId, MetadataRecord? record)
    {
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

    public MetadataStatusSummaryDto BuildSummary(IReadOnlyList<MetadataRecord> records)
        => new(
            records.Count(item => item.State == MetadataState.NeedsMetadata),
            records.Count(item => item.State == MetadataState.Queued),
            records.Count(item => item.State == MetadataState.Scraping),
            records.Count(item => item.State == MetadataState.Ready),
            records.Count(item => item.State == MetadataState.NeedsReview),
            records.Count(item => item.State == MetadataState.Disabled),
            records.Count(item => item.FailureKind == MetadataFailureKind.NetworkError),
            records.Count(item => item.FailureKind == MetadataFailureKind.NoMatch),
            records.Count(item => item.FailureKind == MetadataFailureKind.ProviderError));

    public MetadataFolderStateSummary BuildFolderStateSummary(MetadataDto? metadata)
        => metadata is null
            ? new MetadataFolderStateSummary(false, MetadataState.NeedsMetadata, null, null)
            : new MetadataFolderStateSummary(
                !string.IsNullOrWhiteSpace(metadata.Title) || !string.IsNullOrWhiteSpace(metadata.PosterPath),
                metadata.State,
                metadata.Title,
                metadata.PosterPath);
}
