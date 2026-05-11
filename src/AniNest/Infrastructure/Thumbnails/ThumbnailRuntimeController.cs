using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Persistence;

namespace AniNest.Infrastructure.Thumbnails;

internal sealed class ThumbnailRuntimeController
{
    private static readonly Logger Log = AppLog.For<ThumbnailRuntimeController>();

    private readonly ISettingsService _settings;
    private readonly IThumbnailDecodeStrategyService _decodeStrategyService;
    private readonly ThumbnailWorkerPool _workerPool;
    private readonly ThumbnailWorkerCancellationCoordinator _workerCancellationCoordinator;
    private readonly ThumbnailWorkerSuspensionCoordinator _workerSuspensionCoordinator;
    private readonly ThumbnailCacheMaintenance _cacheMaintenance;
    private readonly ThumbnailStatusTracker _statusTracker;
    private readonly Action _ensureLoopRunning;
    private readonly Action _notifyStatusChanged;
    private readonly Action _saveIndex;
    private readonly Action<ThumbnailTask, ThumbnailState> _setTaskState;
    private readonly Func<string> _buildSchedulerSnapshot;
    private readonly Func<bool> _isPlayerActive;
    private readonly Action<bool> _setPlayerActive;
    private readonly Func<ThumbnailPerformanceMode> _getPerformanceMode;
    private readonly Action<ThumbnailPerformanceMode> _setPerformanceMode;
    private readonly Func<int> _demotePlaybackIntents;
    private readonly Action _clearPlaybackForegroundTarget;

    public ThumbnailRuntimeController(
        ISettingsService settings,
        IThumbnailDecodeStrategyService decodeStrategyService,
        ThumbnailWorkerPool workerPool,
        ThumbnailWorkerCancellationCoordinator workerCancellationCoordinator,
        ThumbnailWorkerSuspensionCoordinator workerSuspensionCoordinator,
        ThumbnailCacheMaintenance cacheMaintenance,
        ThumbnailStatusTracker statusTracker,
        Action ensureLoopRunning,
        Action notifyStatusChanged,
        Action saveIndex,
        Action<ThumbnailTask, ThumbnailState> setTaskState,
        Func<string> buildSchedulerSnapshot,
        Func<bool> isPlayerActive,
        Action<bool> setPlayerActive,
        Func<ThumbnailPerformanceMode> getPerformanceMode,
        Action<ThumbnailPerformanceMode> setPerformanceMode,
        Func<int> demotePlaybackIntents,
        Action clearPlaybackForegroundTarget)
    {
        _settings = settings;
        _decodeStrategyService = decodeStrategyService;
        _workerPool = workerPool;
        _workerCancellationCoordinator = workerCancellationCoordinator;
        _workerSuspensionCoordinator = workerSuspensionCoordinator;
        _cacheMaintenance = cacheMaintenance;
        _statusTracker = statusTracker;
        _ensureLoopRunning = ensureLoopRunning;
        _notifyStatusChanged = notifyStatusChanged;
        _saveIndex = saveIndex;
        _setTaskState = setTaskState;
        _buildSchedulerSnapshot = buildSchedulerSnapshot;
        _isPlayerActive = isPlayerActive;
        _setPlayerActive = setPlayerActive;
        _getPerformanceMode = getPerformanceMode;
        _setPerformanceMode = setPerformanceMode;
        _demotePlaybackIntents = demotePlaybackIntents;
        _clearPlaybackForegroundTarget = clearPlaybackForegroundTarget;
    }

    public void SetPlayerActive(bool isActive)
    {
        bool changed = _isPlayerActive() != isActive;
        _setPlayerActive(isActive);

        string snapshot;
        int demotedPlaybackTasks = 0;
        List<ThumbnailGeneratorWorker> playbackWorkersToCancel = [];
        string playbackWorkerFiles = string.Empty;

        if (!isActive)
        {
            demotedPlaybackTasks = _demotePlaybackIntents();
            _clearPlaybackForegroundTarget();

            playbackWorkersToCancel = _workerPool.SnapshotWorkers()
                .Where(static worker => !worker.Execution.IsCompleted)
                .Where(worker => ThumbnailWorkIntentPriority.IsPlaybackIntent(worker.Task.Intent))
                .ToList();

            playbackWorkerFiles = string.Join(", ",
                playbackWorkersToCancel.Select(worker => Path.GetFileName(worker.Task.VideoPath)));
        }

        snapshot = _buildSchedulerSnapshot();

        if (!changed && demotedPlaybackTasks == 0 && playbackWorkersToCancel.Count == 0)
            return;

        Log.Info(
            $"Thumbnail player activity changed: isActive={isActive}, demotedPlaybackTasks={demotedPlaybackTasks}, " +
            $"cancelPlaybackWorkers={playbackWorkersToCancel.Count}, files=[{playbackWorkerFiles}], {snapshot}");
        _workerCancellationCoordinator.CancelWithReason(playbackWorkersToCancel, "player-inactive", "Thumbnail player playback workers canceled");

        _notifyStatusChanged();
        _ensureLoopRunning();
    }

    public void RefreshPerformanceMode()
    {
        ThumbnailPerformanceMode mode = _settings.GetThumbnailPerformanceMode();
        ThumbnailPerformanceMode previousMode = _getPerformanceMode();
        bool changed = previousMode != mode;
        _setPerformanceMode(mode);

        string snapshot = _buildSchedulerSnapshot();
        if (!changed)
            return;

        Log.Info($"Thumbnail performance mode changed: selectedMode={mode}, {snapshot}");
        bool wasPaused = previousMode == ThumbnailPerformanceMode.Paused;
        bool isPaused = mode == ThumbnailPerformanceMode.Paused;

        if (!wasPaused && isPaused)
            PauseActiveWorkers();
        else if (wasPaused && !isPaused)
            ResumePausedWorkers();
        else
            RequeueActiveWorkers("performance-mode-changed");

        _notifyStatusChanged();
        _ensureLoopRunning();
    }

    public void RefreshDecodeStrategy()
    {
        _decodeStrategyService.RefreshAccelerationMode();

        string snapshot = _buildSchedulerSnapshot();
        Log.Info($"Thumbnail decode strategy refreshed: {snapshot}");
        RequeueActiveWorkers("decode-strategy-changed");
        _notifyStatusChanged();
        _ensureLoopRunning();
    }

    public void Shutdown(
        CancellationTokenSource? loopCts,
        Task? loopTask,
        CancellationTokenSource? expiryCts,
        Action markShuttingDown)
    {
        Log.Info("[Shutdown] starting");

        markShuttingDown();
        loopCts?.Cancel();
        expiryCts?.Cancel();
        CancelActiveWorkersForShutdown();

        if (loopTask != null)
        {
            Log.Info($"[Shutdown] waiting for _loopTask (Status={loopTask.Status})...");
            try { loopTask.Wait(5000); } catch { }
        }

        _cacheMaintenance.CleanupTempArtifacts();
        _saveIndex();
    }

    public void CleanupExpired(ThumbnailTaskStore taskStore)
    {
        if (!_cacheMaintenance.CleanupExpired(taskStore))
            return;

        _statusTracker.UpdateProgress();
    }

    public void RequeueActiveWorkers(string reason)
    {
        List<ThumbnailGeneratorWorker> workersToCancel = _workerPool.MarkAllActiveForCancellation(reason);
        _workerCancellationCoordinator.CancelWithReason(workersToCancel, reason, "Thumbnail active worker requeue");
    }

    public void PauseActiveWorkers()
    {
        ThumbnailGeneratorWorker[] activeWorkers = _workerPool.SnapshotWorkers()
            .Where(static worker => !worker.Execution.IsCompleted)
            .ToArray();

        if (activeWorkers.Length == 0)
            return;

        _workerSuspensionCoordinator.SuspendActiveWorkers(activeWorkers, _setTaskState);
    }

    public void ResumePausedWorkers()
    {
        ThumbnailGeneratorWorker[] pausedWorkers = _workerPool.SnapshotWorkers()
            .Where(static worker => !worker.Execution.IsCompleted)
            .Where(worker => worker.Task.State == ThumbnailState.PausedGenerating)
            .ToArray();

        _workerSuspensionCoordinator.ResumePausedWorkers(pausedWorkers, _setTaskState);
    }

    private void CancelActiveWorkersForShutdown()
    {
        List<ThumbnailGeneratorWorker> workers = _workerPool.MarkAllForShutdown();
        _workerCancellationCoordinator.CancelWithReason(workers, "shutdown", "Thumbnail shutdown workers canceled");
    }
}
