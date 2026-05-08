namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailGeneratorWorker
{
    public required ThumbnailTask Task { get; init; }
    public required Task Execution { get; init; }
}
