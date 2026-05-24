using AniNest.Application.Metadata;

namespace AniNest.Host.Modules;

internal sealed class MetadataTaskScheduler : IMetadataTaskScheduler
{
    private readonly IMetadataTaskQueue _queue;

    public MetadataTaskScheduler(IMetadataTaskQueue queue)
    {
        _queue = queue;
    }

    public bool Enqueue(MetadataTaskPlan plan) => _queue.Enqueue(plan);

    public ValueTask<MetadataTaskPlan> DequeueAsync(CancellationToken cancellationToken)
        => _queue.DequeueAsync(cancellationToken);
}
