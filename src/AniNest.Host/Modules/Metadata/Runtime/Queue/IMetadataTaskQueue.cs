using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal interface IMetadataTaskQueue
{
    bool Enqueue(MetadataTaskPlan plan);
    ValueTask<MetadataTaskPlan> DequeueAsync(CancellationToken cancellationToken);
}
