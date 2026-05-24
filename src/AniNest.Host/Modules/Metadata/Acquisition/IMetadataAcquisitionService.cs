namespace AniNest.Host.Modules;

internal interface IMetadataAcquisitionService
{
    Task<MetadataAcquisitionResult> AcquireAsync(
        MetadataPreparedContext context,
        CancellationToken cancellationToken);
}
