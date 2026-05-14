namespace AniNest.Infrastructure.Thumbnails;

public class ThumbnailProgressEventArgs : EventArgs
{
    public int Ready { get; init; }
    public int Total { get; init; }
}
