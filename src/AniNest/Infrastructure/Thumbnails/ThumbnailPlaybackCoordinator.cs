using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AniNest.Infrastructure.Logging;

namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailPlaybackCoordinator
{
    private static readonly Logger Log = AppLog.For<ThumbnailPlaybackCoordinator>();

    private readonly ThumbnailTaskStore _taskStore;
    private readonly ThumbnailWorkerPool _workerPool;
    private readonly Action _ensureLoopRunning;
    private readonly Action _notifyStatusChanged;
    private readonly Action<ThumbnailWorkIntent> _preemptLowerPriorityWorkers;
    private readonly Action<string, string?> _preemptStalePlaybackWorkers;

    public ThumbnailPlaybackCoordinator(
        ThumbnailTaskStore taskStore,
        ThumbnailWorkerPool workerPool,
        Action ensureLoopRunning,
        Action notifyStatusChanged,
        Action<ThumbnailWorkIntent> preemptLowerPriorityWorkers,
        Action<string, string?> preemptStalePlaybackWorkers)
    {
        _taskStore = taskStore;
        _workerPool = workerPool;
        _ensureLoopRunning = ensureLoopRunning;
        _notifyStatusChanged = notifyStatusChanged;
        _preemptLowerPriorityWorkers = preemptLowerPriorityWorkers;
        _preemptStalePlaybackWorkers = preemptStalePlaybackWorkers;
    }

    public void BoostVideo(string videoPath)
    {
        bool shouldPreempt = false;
        IntentApplyOutcome outcome = IntentApplyOutcome.MissingTask;
        if (_taskStore.TryGetTask(videoPath, out var task))
        {
            outcome = _taskStore.ApplyIntentToVideo(videoPath, ThumbnailWorkIntent.ManualSingle, task.SourceCollectionId, DateTime.UtcNow.Ticks);
            _taskStore.CurrentForegroundTargetVideoPath = task.VideoPath;
            _taskStore.CurrentForegroundTargetIntent = task.Intent.ToString();
            shouldPreempt = ThumbnailWorkerPreemption.ShouldPreemptForIncomingIntent(
                _workerPool.SnapshotWorkers(),
                ThumbnailWorkIntent.ManualSingle);
        }

        Log.Info($"Thumbnail video boosted: file={Path.GetFileName(videoPath)}, outcome={outcome}, shouldPreempt={shouldPreempt}");
        if (shouldPreempt)
            _preemptLowerPriorityWorkers(ThumbnailWorkIntent.ManualSingle);
        _ensureLoopRunning();
        _notifyStatusChanged();
    }

    public void BoostPlaybackWindow(IReadOnlyList<string> orderedVideoPaths, int currentIndex, int lookaheadCount)
    {
        if (orderedVideoPaths.Count == 0 || currentIndex < 0 || currentIndex >= orderedVideoPaths.Count)
            return;

        ThumbnailPlaybackWindowUpdate update = ThumbnailPlaybackWindowCoordinator.Apply(
            _taskStore,
            _workerPool.SnapshotWorkers(),
            orderedVideoPaths,
            currentIndex,
            lookaheadCount,
            DateTime.UtcNow.Ticks);

        Log.Info(
            $"Thumbnail playback window boost: currentIndex={currentIndex}, lookahead={lookaheadCount}, currentFile={Path.GetFileName(update.CurrentVideoPath)}, keepFile={Path.GetFileName(update.KeepPlaybackWorkerVideoPath ?? string.Empty)}, " +
            $"candidateWindow=[{update.CandidateWindowSummary}], currentOutcome={update.CurrentOutcome}, nearbyApplied={update.NearbyApplied}, nearbyReady={update.NearbyReady}, nearbyHigherIntent={update.NearbyHigherIntent}, nearbyMissing={update.NearbyMissing}, lowerPriorityPreemptionIntent={update.LowerPriorityPreemptionIntent?.ToString() ?? "-"}, shouldPreemptLowerPriority={update.ShouldPreemptLowerPriority}, stalePlaybackWorkers={update.StalePlaybackWorkers}");
        if (update.StalePlaybackWorkers > 0)
            _preemptStalePlaybackWorkers(update.CurrentVideoPath, update.KeepPlaybackWorkerVideoPath);
        else if (update.ShouldPreemptLowerPriority && update.LowerPriorityPreemptionIntent.HasValue)
            _preemptLowerPriorityWorkers(update.LowerPriorityPreemptionIntent.Value);
        _ensureLoopRunning();
        _notifyStatusChanged();
    }
}
