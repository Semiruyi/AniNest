using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal interface IMetadataReadyStateService
{
    MetadataRecord SaveReady(
        MetadataRecord record,
        string? sourceId,
        MetadataAssetSnapshot assets);
}
