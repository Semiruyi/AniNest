using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AniNest.Infrastructure.Diagnostics;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Interop;
namespace AniNest.Infrastructure.Thumbnails;

public enum ThumbnailState
{
    Pending,
    Generating,
    Ready,
    Failed
}

public class ThumbnailTask
{
    public string VideoPath { get; init; } = "";
    public string Md5Dir { get; set; } = "";
    public ThumbnailState State { get; set; } = ThumbnailState.Pending;
    public int TotalFrames { get; set; }
    public long MarkedForDeletionAt { get; set; }
    public ThumbnailWorkIntent Intent { get; set; } = ThumbnailWorkIntent.BackgroundFill;
    public string? SourceCollectionId { get; set; }
    public long IntentUpdatedAtUtcTicks { get; set; }
}

public class ThumbnailProgressEventArgs : EventArgs
{
    public int Ready { get; init; }
    public int Total { get; init; }
}

internal enum IntentApplyOutcome
{
    Applied,
    AlreadyReady,
    HigherIntentAlreadyPresent,
    MissingTask
}

public class ThumbnailGenerator : IThumbnailGenerator, IDisposable
{
    private static readonly Logger Log = AppLog.For<ThumbnailGenerator>();

    // Dependencies
    private readonly ISettingsService _settings;
    private readonly IThumbnailDecodeStrategyService _decodeStrategyService;

    // Paths
    private readonly string _thumbBaseDir;
    private readonly string _ffmpegPath;

    // Queue & state
    private readonly ThumbnailTaskStore _taskStore = new();
    private readonly ThumbnailWorkerPool _workerPool = new();
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private TaskCompletionSource _initTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ThumbnailIndexRepository _indexRepository;
    private readonly ThumbnailCacheMaintenance _cacheMaintenance;
    private readonly ThumbnailGenerationRunner _generationRunner;
    private readonly ThumbnailWorkerExecutionHost _workerExecutionHost;
    private readonly ThumbnailWorkerCancellationCoordinator _workerCancellationCoordinator;
    private readonly ThumbnailStatusTracker _statusTracker;
    private bool _isShuttingDown;
    private bool _isPlayerActive;
    private bool _isGenerationPaused;
    private ThumbnailPerformanceMode _performanceMode;
    private string? _lastSchedulerState;

    // Events
    public event EventHandler<ThumbnailProgressEventArgs>? ProgressChanged;
    public event Action<string, int>? VideoProgress; // videoPath, percent 0-100
    public event Action<string>? VideoReady; // videoPath
    public event Action? StatusChanged;

    // Detection
    private bool _ffmpegAvailable;

    public bool IsFfmpegAvailable => _ffmpegAvailable;

    public ThumbnailGenerationStatusSnapshot GetStatusSnapshot()
        => _statusTracker.CreateSnapshot(_isGenerationPaused, _isPlayerActive, _workerPool.Count);

    public ThumbnailGenerator(
        ISettingsService settings,
        IThumbnailDecodeStrategyService decodeStrategyService)
    {
        _settings = settings;
        _decodeStrategyService = decodeStrategyService;
        _performanceMode = _settings.GetThumbnailPerformanceMode();
        _isGenerationPaused = _settings.IsThumbnailGenerationPaused();
        _thumbBaseDir = AppPaths.ThumbnailDirectory;
        _ffmpegPath = AppPaths.FfmpegPath;
        _indexRepository = new ThumbnailIndexRepository(_thumbBaseDir);
        _cacheMaintenance = new ThumbnailCacheMaintenance(_indexRepository, _settings);
        _statusTracker = new ThumbnailStatusTracker(
            _taskStore,
            args => ProgressChanged?.Invoke(this, args),
            () => StatusChanged?.Invoke());
        var renderer = new ThumbnailRenderer(_ffmpegPath, _thumbBaseDir, GetVideoDuration);
        _generationRunner = new ThumbnailGenerationRunner(_decodeStrategyService, renderer);
        _workerExecutionHost = new ThumbnailWorkerExecutionHost(
            _generationRunner,
            _taskStore,
            SetTaskState,
            SaveIndex,
            _statusTracker.UpdateProgress,
            BuildSchedulerSnapshotUnsafe,
            (path, percent) => VideoProgress?.Invoke(path, percent),
            path => VideoReady?.Invoke(path));
        _workerCancellationCoordinator = new ThumbnailWorkerCancellationCoordinator(BuildSchedulerSnapshotUnsafe);

        Directory.CreateDirectory(_thumbBaseDir);

        Task.Run(Initialize);
    }

    private void Initialize()
    {
        var sw = Stopwatch.StartNew();

        _ffmpegAvailable = File.Exists(_ffmpegPath);
        Log.Info($"[Init] ffmpegPath={_ffmpegPath}, exists={_ffmpegAvailable}");
        if (_ffmpegAvailable)
        {
            try
            {
                var versionProc = Process.Start(new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = "-version",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                });
                versionProc?.WaitForExit(3000);
                if (versionProc != null)
                {
                    var verOutput = versionProc.StandardOutput.ReadLine();
                    versionProc.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Info($"[Init] ffmpeg version probe failed: {ex.Message}");
            }
        }
        else
        {
            Log.Info("[Init] ffmpeg not available, thumbnail generation disabled");
        }

        _cacheMaintenance.CleanupTempArtifacts();

        LoadIndex();

        sw.Stop();

        _initTcs.TrySetResult();
        EnsureLoopRunning();
        StartExpiryCleanup();
    }


    public void RegisterCollection(LibraryCollectionRef collection, IReadOnlyCollection<string> videoPaths)
    {
        if (!_ffmpegAvailable)
            return;

        _taskStore.RegisterCollection(collection, videoPaths, ComputeMd5);

        Log.Info($"Thumbnail collection registered: id={collection.Id}, kind={collection.Kind}, name={collection.Name}, videos={videoPaths.Count}");
        EnsureLoopRunning();
        NotifyStatusChanged();
    }

    public void RemoveCollection(string collectionId)
    {
        _taskStore.RemoveCollection(collectionId);
    }

    public void FocusCollection(string collectionId)
    {
        int promotedCount = _taskStore.ApplyIntentToCollection(collectionId, ThumbnailWorkIntent.FocusedCollection);
        bool shouldPreempt = ThumbnailWorkerPreemption.ShouldPreemptForIncomingIntent(
            _workerPool.SnapshotWorkers(),
            ThumbnailWorkIntent.FocusedCollection);

        Log.Info($"Thumbnail collection focused: id={collectionId}, promoted={promotedCount}, shouldPreempt={shouldPreempt}");
        if (shouldPreempt)
            PreemptLowerPriorityWorkers(ThumbnailWorkIntent.FocusedCollection);
        EnsureLoopRunning();
        NotifyStatusChanged();
    }

    public void BoostCollection(string collectionId)
    {
        int promotedCount = _taskStore.ApplyIntentToCollection(collectionId, ThumbnailWorkIntent.ManualCollection);
        bool shouldPreempt = ThumbnailWorkerPreemption.ShouldPreemptForIncomingIntent(
            _workerPool.SnapshotWorkers(),
            ThumbnailWorkIntent.ManualCollection);

        Log.Info($"Thumbnail collection boosted: id={collectionId}, promoted={promotedCount}, shouldPreempt={shouldPreempt}");
        if (shouldPreempt)
            PreemptLowerPriorityWorkers(ThumbnailWorkIntent.ManualCollection);
        EnsureLoopRunning();
        NotifyStatusChanged();
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
            PreemptLowerPriorityWorkers(ThumbnailWorkIntent.ManualSingle);
        EnsureLoopRunning();
        NotifyStatusChanged();
    }

    public void BoostPlaybackWindow(IReadOnlyList<string> orderedVideoPaths, int currentIndex, int lookaheadCount)
    {
        if (orderedVideoPaths.Count == 0 || currentIndex < 0 || currentIndex >= orderedVideoPaths.Count)
            return;

        ThumbnailPlaybackWindowUpdate update;
        update = ThumbnailPlaybackWindowCoordinator.Apply(
            _taskStore,
            _workerPool.SnapshotWorkers(),
            orderedVideoPaths,
            currentIndex,
            lookaheadCount,
            DateTime.UtcNow.Ticks);

        Log.Info(
            $"Thumbnail playback window boost: currentIndex={currentIndex}, lookahead={lookaheadCount}, currentFile={Path.GetFileName(update.CurrentVideoPath)}, keepFile={Path.GetFileName(update.KeepPlaybackWorkerVideoPath ?? string.Empty)}, " +
            $"candidateWindow=[{update.CandidateWindowSummary}], currentOutcome={update.CurrentOutcome}, nearbyApplied={update.NearbyApplied}, nearbyReady={update.NearbyReady}, nearbyHigherIntent={update.NearbyHigherIntent}, nearbyMissing={update.NearbyMissing}, shouldPreemptLowerPriority={update.ShouldPreemptLowerPriority}, stalePlaybackWorkers={update.StalePlaybackWorkers}");
        if (update.StalePlaybackWorkers > 0)
            PreemptStalePlaybackWorkers(update.CurrentVideoPath, update.KeepPlaybackWorkerVideoPath);
        else if (update.ShouldPreemptLowerPriority)
            PreemptLowerPriorityWorkers(ThumbnailWorkIntent.PlaybackCurrent);
        EnsureLoopRunning();
        NotifyStatusChanged();
    }

    public void ResetCollection(string collectionId, bool boostAfterReset)
    {
        long updatedAtTicks = DateTime.UtcNow.Ticks;
        _taskStore.ResetCollection(collectionId, boostAfterReset, updatedAtTicks,
            out var thumbnailDirsToDelete, out bool changed, out bool shouldPreempt,
            out _, out _);

        foreach (string thumbnailDir in thumbnailDirsToDelete.Distinct(StringComparer.OrdinalIgnoreCase))
            _indexRepository.DeleteThumbnailDirectory(thumbnailDir);

        if (!changed)
            return;

        if (shouldPreempt)
            PreemptLowerPriorityWorkers(ThumbnailWorkIntent.ManualCollection);
        SaveIndex();
        EnsureLoopRunning();
        NotifyStatusChanged();
    }

    public void EnqueueFolder(string folderPath, IReadOnlyCollection<string> videoFiles, int cardOrder,
        string? lastPlayedPath, HashSet<string> playedPaths)
    {
        RegisterCollection(new LibraryCollectionRef(folderPath, LibraryCollectionKind.Folder, Path.GetFileName(folderPath)), videoFiles);
    }

    public void DeleteForFolder(string folderPath, IReadOnlyCollection<string>? videoFiles = null)
    {
        _taskStore.DeleteForFolder(folderPath, videoFiles);

        RemoveCollection(folderPath);
        SaveIndex();
        NotifyStatusChanged();
    }

    public void SetPlayerActive(bool isActive)
    {
        bool changed;
        string snapshot;
        int demotedPlaybackTasks = 0;
        List<ThumbnailGeneratorWorker> playbackWorkersToCancel = [];
        string playbackWorkerFiles = string.Empty;

        changed = _isPlayerActive != isActive;
        _isPlayerActive = isActive;

        if (!isActive)
        {
            demotedPlaybackTasks = DemotePlaybackIntentsUnsafe();
            ClearPlaybackForegroundTargetUnsafe();

            playbackWorkersToCancel = _workerPool.SnapshotWorkers()
                .Where(static worker => !worker.Execution.IsCompleted)
                .Where(worker => ThumbnailWorkIntentPriority.IsPlaybackIntent(worker.Task.Intent))
                .ToList();

            playbackWorkerFiles = string.Join(", ",
                playbackWorkersToCancel.Select(worker => Path.GetFileName(worker.Task.VideoPath)));
        }

        snapshot = BuildSchedulerSnapshotUnsafe();

        if (!changed && demotedPlaybackTasks == 0 && playbackWorkersToCancel.Count == 0)
            return;

        Log.Info(
            $"Thumbnail player activity changed: isActive={isActive}, demotedPlaybackTasks={demotedPlaybackTasks}, " +
            $"cancelPlaybackWorkers={playbackWorkersToCancel.Count}, files=[{playbackWorkerFiles}], {snapshot}");
        _workerCancellationCoordinator.CancelWithReason(playbackWorkersToCancel, "player-inactive", "Thumbnail player playback workers canceled");

        NotifyStatusChanged();
        EnsureLoopRunning();
    }

    public void RefreshPerformanceMode()
    {
        ThumbnailPerformanceMode mode = _settings.GetThumbnailPerformanceMode();
        string snapshot;
        bool changed;

        changed = _performanceMode != mode;
        _performanceMode = mode;
        snapshot = BuildSchedulerSnapshotUnsafe();

        if (!changed)
            return;

        Log.Info($"Thumbnail performance mode changed: selectedMode={mode}, {snapshot}");
        RequeueActiveWorkers("performance-mode-changed");
        NotifyStatusChanged();
        EnsureLoopRunning();
    }

    public void RefreshGenerationPaused()
    {
        bool paused = _settings.IsThumbnailGenerationPaused();
        bool changed;
        string snapshot;

        changed = _isGenerationPaused != paused;
        _isGenerationPaused = paused;
        snapshot = BuildSchedulerSnapshotUnsafe();

        if (!changed)
            return;

        Log.Info($"Thumbnail generation paused changed: paused={paused}, {snapshot}");
        if (paused)
            RequeueActiveWorkers("generation-paused");
        NotifyStatusChanged();
        EnsureLoopRunning();
    }

    public void RefreshDecodeStrategy()
    {
        _decodeStrategyService.RefreshAccelerationMode();

        string snapshot = BuildSchedulerSnapshotUnsafe();

        Log.Info($"Thumbnail decode strategy refreshed: {snapshot}");
        RequeueActiveWorkers("decode-strategy-changed");
        NotifyStatusChanged();
        EnsureLoopRunning();
    }

    public ThumbnailState GetState(string videoPath)
    {
        using var span = PerfSpan.Begin("Thumbnail.GetState", new Dictionary<string, string>
        {
            ["file"] = Path.GetFileName(videoPath)
        });
        return _taskStore.GetState(videoPath);
    }

    public byte[]? GetThumbnailBytes(string videoPath, long positionMs)
    {
        if (!_ffmpegAvailable) return null;

        _taskStore.TryGetTask(videoPath, out var task);

        if (task == null || task.State != ThumbnailState.Ready)
            return null;

        string directory = Path.Combine(_thumbBaseDir, task.Md5Dir);
        int? frameIndex = ThumbnailFrameIndex.ResolveFrameIndex(directory, positionMs);
        if (frameIndex == null)
            return null;

        return ThumbnailBundle.ReadFrameBytes(directory, frameIndex.Value);
    }


    private void EnsureLoopRunning()
    {
        if (_loopTask != null && !_loopTask.IsCompleted) return;
        if (_isShuttingDown) return;

        _loopCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => ProcessQueueLoop(_loopCts.Token));
    }

    private async Task ProcessQueueLoop(CancellationToken ct)
    {
        await _initTcs.Task;
        while (!ct.IsCancellationRequested && !_isShuttingDown)
        {
            DrainCompletedWorkers();

            bool hasPending = _taskStore.CountTasksByState(ThumbnailState.Pending) > 0;
            bool canStartWorkers = ThumbnailQueueScheduler.CanStartMoreWorkers(
                _workerPool,
                _isGenerationPaused,
                _performanceMode,
                _isPlayerActive);

            var task = ThumbnailQueueScheduler.SelectNextTask(
                _taskStore,
                _workerPool,
                _isGenerationPaused,
                _performanceMode,
                _isPlayerActive);
            if (task != null)
            {
                ReportSchedulerState("starting-workers");
                StartWorker(task, ct);
                continue;
            }

            if (!hasPending && GetActiveWorkerCount() == 0)
            {
                ReportSchedulerState("idle");
                try { await Task.Delay(2000, ct); } catch { break; }
                continue;
            }

            ReportSchedulerState(canStartWorkers ? "waiting-for-pending-selection" : GetBlockedSchedulerReason());
            int waitDelayMs = canStartWorkers ? 200 : 500;
            try { await Task.Delay(waitDelayMs, ct); } catch { break; }
        }

        await WaitForWorkersAsync();
    }

    private void StartWorker(ThumbnailTask task, CancellationToken ct)
    {
        var workerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var worker = new ThumbnailGeneratorWorker
        {
            Task = task,
            Execution = Task.CompletedTask,
            Cancellation = workerCts
        };
        Task execution = RunWorkerAsync(worker, workerCts.Token);
        worker.Execution = execution;
        _workerPool.Add(worker);
        string snapshot = BuildSchedulerSnapshotUnsafe();

        Log.Info(
            $"Thumbnail worker start: file={Path.GetFileName(task.VideoPath)}, intent={task.Intent}, {snapshot}");
        NotifyStatusChanged();
    }

    private async Task RunWorkerAsync(ThumbnailGeneratorWorker worker, CancellationToken ct)
    {
        await _workerExecutionHost.RunAsync(worker, ct);
        NotifyStatusChanged();
    }

    private void DrainCompletedWorkers()
    {
        bool changed = _workerPool.DrainCompletedWorkers();

        if (changed)
            NotifyStatusChanged();
    }

    private int GetActiveWorkerCount() => _workerPool.Count;

    private string BuildSchedulerSnapshotUnsafe()
        => ThumbnailQueueScheduler.BuildSnapshot(_workerPool, _taskStore, _isGenerationPaused, _isPlayerActive, _performanceMode);

    private void ReportSchedulerState(string state)
    {
        string snapshot;
        string message;

        snapshot = BuildSchedulerSnapshotUnsafe();
        message = $"{state} | {snapshot}";
        if (string.Equals(_lastSchedulerState, message, StringComparison.Ordinal))
            return;

        _lastSchedulerState = message;

        Log.Info($"Thumbnail scheduler state: {message}");
    }

    private string GetBlockedSchedulerReason()
        => ThumbnailQueueScheduler.GetBlockedReason(_workerPool, _isGenerationPaused, _performanceMode, _isPlayerActive);

    private void RequeueActiveWorkers(string reason)
    {
        List<ThumbnailGeneratorWorker> workersToCancel;
        workersToCancel = _workerPool.MarkAllActiveForCancellation(reason);
        _workerCancellationCoordinator.CancelWithReason(workersToCancel, reason, "Thumbnail active worker requeue");
    }

    private void PreemptLowerPriorityWorkers(ThumbnailWorkIntent incomingIntent)
    {
        List<ThumbnailGeneratorWorker> workersToCancel;
        workersToCancel = _workerPool.MarkForCancellation(
            workers => ThumbnailWorkerPreemption.SelectLowerPriorityWorkers(workers, incomingIntent),
            worker => $"preempted-by-{incomingIntent}");
        _workerCancellationCoordinator.CancelWithComputedReasons(
            workersToCancel,
            worker => $"preempted-by-{incomingIntent}",
            "Thumbnail worker preempt");
    }

    private void PreemptStalePlaybackWorkers(string currentVideoPath, string? keepPlaybackWorkerVideoPath)
    {
        List<ThumbnailGeneratorWorker> workersToCancel;
        workersToCancel = _workerPool.MarkForCancellation(
            workers => ThumbnailWorkerPreemption.SelectStalePlaybackWorkers(
                workers,
                currentVideoPath,
                keepPlaybackWorkerVideoPath),
            worker => $"stale-playback-target:{Path.GetFileName(currentVideoPath)}");
        _workerCancellationCoordinator.CancelWithComputedReasons(
            workersToCancel,
            worker => $"stale-playback-target:{Path.GetFileName(currentVideoPath)}",
            $"Thumbnail stale playback worker preempt: currentFile={Path.GetFileName(currentVideoPath)}, keepFile={Path.GetFileName(keepPlaybackWorkerVideoPath ?? string.Empty)}");
    }

    private async Task WaitForWorkersAsync()
    {
        Task[] workers = _workerPool.SnapshotExecutionTasks();

        if (workers.Length == 0)
            return;

        try
        {
            await Task.WhenAll(workers);
        }
        catch
        {
        }
    }

    private void CancelActiveWorkersForShutdown()
    {
        List<ThumbnailGeneratorWorker> workers = _workerPool.MarkAllForShutdown();
        _workerCancellationCoordinator.CancelWithReason(workers, "shutdown", "Thumbnail shutdown workers canceled");
    }


    public void Shutdown()
    {
        var sw = Stopwatch.StartNew();
        Log.Info("[Shutdown] starting");

        _isShuttingDown = true;

        _loopCts?.Cancel();
        _expiryCts?.Cancel();
        CancelActiveWorkersForShutdown();

        if (_loopTask != null)
        {
        Log.Info($"[Shutdown] waiting for _loopTask (Status={_loopTask.Status})...");
            var waitSw = Stopwatch.StartNew();
            try { _loopTask.Wait(5000); } catch { }
            waitSw.Stop();
        }

        _cacheMaintenance.CleanupTempArtifacts();

        SaveIndex();
        sw.Stop();
    }


    private CancellationTokenSource? _expiryCts;

    private void StartExpiryCleanup()
    {
        _expiryCts = new CancellationTokenSource();
        _ = ExpiryCleanupLoop(_expiryCts.Token);
    }

    private async Task ExpiryCleanupLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromHours(1), ct); } catch { break; }
            CleanupExpired();
        }
    }

    private void CleanupExpired()
    {
        if (!_cacheMaintenance.CleanupExpired(_taskStore))
            return;

        _statusTracker.UpdateProgress();
    }


    private void LoadIndex()
        => _cacheMaintenance.LoadInto(_taskStore);

    private void SaveIndex()
        => _cacheMaintenance.SaveFrom(_taskStore);

    private void NotifyStatusChanged()
        => _statusTracker.NotifyStatusChanged();


    private static double GetVideoDuration(string videoPath)
    {
        try
        {
            string ffmpeg = AppPaths.FfmpegPath;
            if (!File.Exists(ffmpeg)) return 0;
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = $"-i \"{videoPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return 0;
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(5000);
            // ffmpeg stderr: "  Duration: 00:24:00.04, start: ..."
            int idx = stderr.IndexOf("Duration:", StringComparison.Ordinal);
            if (idx >= 0)
            {
                int start = idx + 9;
                while (start < stderr.Length && stderr[start] == ' ') start++;
                int end = stderr.IndexOf(',', start);
                if (end > start)
                {
                    string dur = stderr.Substring(start, end - start).Trim();
                    if (TimeSpan.TryParse(dur, out var ts))
                    {
                        return ts.TotalSeconds;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Parse video duration failed", ex);
        }
        return 0;
    }

    private static string ComputeMd5(string input)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLower();
    }

    public void Dispose()
    {
        Shutdown();
        _loopCts?.Dispose();
    }

    private int GetTaskCount()
    {
        return _taskStore.TotalCount;
    }

    internal int CountTasksByState(ThumbnailState state)
        => _taskStore.CountTasksByState(state);

    internal IReadOnlyList<string> GetTaskVideoPathsInOrder()
        => _taskStore.GetTaskVideoPathsInOrder();

    internal ThumbnailWorkIntent? GetIntent(string videoPath)
        => _taskStore.GetIntent(videoPath);

    internal void ForceTaskState(string videoPath, ThumbnailState state)
        => _taskStore.ForceTaskState(videoPath, state);

    internal void RequeueActiveWorkersForTest(string reason)
        => RequeueActiveWorkers(reason);

    internal bool TryRequeueTaskForTest(string videoPath)
        => _taskStore.TryRequeueTask(videoPath);

    internal void PreemptLowerPriorityWorkersForTest(ThumbnailWorkIntent incomingIntent)
        => PreemptLowerPriorityWorkers(incomingIntent);

    internal void AddActiveWorkerForTest(string videoPath, string cancellationReason = "test-worker")
    {
        if (!_taskStore.TryGetTask(videoPath, out var task))
            return;

        task.State = ThumbnailState.Generating;
        _workerPool.AddTestWorker(task, cancellationReason);
    }

    internal bool IsActiveWorkerCancellationRequestedForTest(string videoPath)
        => _workerPool.IsCancellationRequested(videoPath);

    internal void SimulateCanceledActiveWorkerForTest(string videoPath)
    {
        _taskStore.TryRequeueTask(videoPath);
    }

    private void SetTaskState(ThumbnailTask task, ThumbnailState newState)
        => _taskStore.SetTaskState(task, newState);

    private int CountForegroundPendingUnsafe()
        => _taskStore.CountForegroundPending();

    private int DemotePlaybackIntentsUnsafe()
        => _taskStore.DemotePlaybackIntents();

    private void ClearPlaybackForegroundTargetUnsafe()
        => _taskStore.ClearPlaybackForegroundTarget();

}





