using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal interface IMetadataPendingStateService
{
    MetadataRecord SavePending(
        MetadataRecord record,
        MetadataResolutionResult resolution);
}
