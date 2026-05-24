using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal interface IMetadataResolutionService
{
    MetadataResolutionResult Resolve(
        MetadataRecord record,
        MetadataPreparedContext context,
        MetadataAcquisitionResult acquisition,
        MetadataConfidenceResult confidence);
}
