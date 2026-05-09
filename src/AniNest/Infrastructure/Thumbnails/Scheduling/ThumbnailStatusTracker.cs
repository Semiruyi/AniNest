namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailStatusTracker
{
    private readonly ThumbnailTaskStore _taskStore;
    private readonly Action<ThumbnailProgressEventArgs> _onProgressChanged;
    private readonly Action _onStatusChanged;

    public ThumbnailStatusTracker(
        ThumbnailTaskStore taskStore,
        Action<ThumbnailProgressEventArgs> onProgressChanged,
        Action onStatusChanged)
    {
        _taskStore = taskStore;
        _onProgressChanged = onProgressChanged;
        _onStatusChanged = onStatusChanged;
    }

    public ThumbnailGenerationStatusSnapshot CreateSnapshot(bool isPaused, bool isPlayerActive, int activeWorkers)
        => _taskStore.CreateSnapshot(isPaused, isPlayerActive, activeWorkers);

    public void UpdateProgress()
    {
        _onProgressChanged(new ThumbnailProgressEventArgs
        {
            Ready = _taskStore.ReadyCount,
            Total = _taskStore.TotalCount
        });

        _onStatusChanged();
    }

    public void NotifyStatusChanged()
        => _onStatusChanged();
}
