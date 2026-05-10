namespace AniNest.Infrastructure.Thumbnails;

internal interface IThumbnailProcessController
{
    void Suspend(int processId);

    void Resume(int processId);
}
