using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal interface IMetadataFetchPipeline
{
    Task<MetadataResolutionResult> ExecuteAsync(
        MetadataRecord record,
        CancellationToken cancellationToken);
}
