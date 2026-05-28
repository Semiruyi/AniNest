using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal sealed class MetadataPendingStateService : IMetadataPendingStateService
{
    private readonly IMetadataRecordStore _recordStore;
    private readonly IMetadataReviewStore _reviewStore;
    private readonly IMetadataAssetService _assets;

    public MetadataPendingStateService(
        IMetadataRecordStore recordStore,
        IMetadataReviewStore reviewStore,
        IMetadataAssetService assets)
    {
        _recordStore = recordStore;
        _reviewStore = reviewStore;
        _assets = assets;
    }

    public MetadataRecord SavePending(
        MetadataRecord record,
        MetadataResolutionResult resolution)
    {
        _assets.DeleteAssets(record.MetadataFilePath, record.PosterFilePath);

        if (resolution.ReviewRecord is not null)
            _reviewStore.Save(resolution.ReviewRecord);
        else
            _reviewStore.Delete(record.FolderId);

        var pendingRecord = record with
        {
            State = resolution.NextState,
            FailureKind = resolution.FailureKind,
            SourceId = resolution.SourceId,
            MetadataFilePath = null,
            PosterFilePath = null
        };

        _recordStore.Save(pendingRecord);
        return pendingRecord;
    }
}
