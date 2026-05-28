using AniNest.Application.Metadata;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal sealed class MetadataReadyStateService : IMetadataReadyStateService
{
    private readonly IMetadataRecordStore _recordStore;
    private readonly IMetadataLegacySyncService _legacySync;

    public MetadataReadyStateService(
        IMetadataRecordStore recordStore,
        IMetadataLegacySyncService legacySync)
    {
        _recordStore = recordStore;
        _legacySync = legacySync;
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
        _legacySync.SaveResolved(completedRecord, assets.Payload);
        return completedRecord;
    }
}
