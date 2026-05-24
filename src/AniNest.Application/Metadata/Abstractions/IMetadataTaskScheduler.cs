namespace AniNest.Application.Metadata;

public interface IMetadataTaskScheduler
{
    bool Enqueue(MetadataTaskPlan plan);
    ValueTask<MetadataTaskPlan> DequeueAsync(CancellationToken cancellationToken);
}
