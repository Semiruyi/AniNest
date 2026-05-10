using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

    public ThumbnailGenerationStatusSnapshot CreateSnapshot(
        bool isPaused,
        bool isPlayerActive,
        int activeWorkers,
        IReadOnlyDictionary<string, int> videoProgressSnapshot,
        IReadOnlyCollection<ThumbnailGeneratorWorker> activeWorkersSnapshot)
        => _taskStore.CreateSnapshot(
            isPaused,
            isPlayerActive,
            activeWorkers,
            BuildActiveTaskSnapshots(videoProgressSnapshot, activeWorkersSnapshot));

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

    private static IReadOnlyList<ThumbnailActiveTaskSnapshot> BuildActiveTaskSnapshots(
        IReadOnlyDictionary<string, int> videoProgressSnapshot,
        IReadOnlyCollection<ThumbnailGeneratorWorker> activeWorkersSnapshot)
        => activeWorkersSnapshot
            .Where(worker => !worker.Execution.IsCompleted)
            .Select(worker => new ThumbnailActiveTaskSnapshot(
                worker.Task.VideoPath,
                Path.GetFileName(worker.Task.VideoPath),
                worker.Task.Intent,
                worker.Task.State,
                videoProgressSnapshot.TryGetValue(worker.Task.VideoPath, out int percent) ? percent : 0,
                ThumbnailWorkIntentPriority.IsPlaybackIntent(worker.Task.Intent),
                worker.IsSuspended))
            .OrderByDescending(static task => ThumbnailWorkIntentPriority.GetRank(task.Intent))
            .ThenByDescending(static task => task.ProgressPercent)
            .ThenBy(static task => task.VideoName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
