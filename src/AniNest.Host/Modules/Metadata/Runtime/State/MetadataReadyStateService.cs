using AniNest.Application.Metadata;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal sealed class MetadataReadyStateService : IMetadataReadyStateService
{
    private readonly IMetadataRecordStore _recordStore;

    public MetadataReadyStateService(IMetadataRecordStore recordStore)
    {
        _recordStore = recordStore;
    }

    public MetadataRecord SaveReady(
        MetadataRecord record,
        string? sourceId,
        MetadataAssetSnapshot assets)
    {
        var completedRecord = record with
        {
            State = MetadataState.Ready,
            FailureKind = MetadataFailureKind.None,
            SourceId = sourceId,
            MetadataFilePath = assets.PayloadPath,
            PosterFilePath = assets.PosterFilePath,
            LastSucceededAtUtc = DateTime.UtcNow
        };

        _recordStore.Save(completedRecord);
        return completedRecord;
    }
}
