namespace AniNest.Host.Modules;

internal interface IMetadataConfidenceService
{
    MetadataConfidenceResult Evaluate(
        MetadataPreparedContext context,
        MetadataAcquisitionResult acquisition);
}
