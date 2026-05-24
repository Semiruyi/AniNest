using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal interface IMetadataPreparationService
{
    Task<MetadataPreparedContext> PrepareAsync(
        MetadataRecord record,
        CancellationToken cancellationToken);
}
