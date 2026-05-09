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
    private readonly List<ThumbnailGeneratorWorker> _activeWorkers = new();
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private TaskCompletionSource _initTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ThumbnailIndexRepository _indexRepository;
    private readonly ThumbnailRenderer _renderer;
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
        => _taskStore.CreateSnapshot(_isGenerationPaused, _isPlayerActive, _activeWorkers.Count);

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
        _renderer = new ThumbnailRenderer(_ffmpegPath, _thumbBaseDir, GetVideoDuration);

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

        _indexRepository.CleanupTempArtifacts();

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
        bool shouldPreempt = ShouldPreemptActiveWorkersUnsafe(ThumbnailWorkIntent.FocusedCollection);

        Log.Info($"Thumbnail collection focused: id={collectionId}, promoted={promotedCount}, shouldPreempt={shouldPreempt}");
        if (shouldPreempt)
            PreemptLowerPriorityWorkers(ThumbnailWorkIntent.FocusedCollection);
        EnsureLoopRunning();
        NotifyStatusChanged();
    }

    public void BoostCollection(string collectionId)
    {
        int promotedCount = _taskStore.ApplyIntentToCollection(collectionId, ThumbnailWorkIntent.ManualCollection);
        bool shouldPreempt = ShouldPreemptActiveWorkersUnsafe(ThumbnailWorkIntent.ManualCollection);

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
            shouldPreempt = ShouldPreemptActiveWorkersUnsafe(ThumbnailWorkIntent.ManualSingle);
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

        string currentVideoPath = orderedVideoPaths[currentIndex];
        long updatedAtTicks = DateTime.UtcNow.Ticks;
        bool shouldPreemptLowerPriority;
        int stalePlaybackWorkers;
        IntentApplyOutcome currentOutcome;
        int nearbyApplied = 0;
        int nearbyReady = 0;
        int nearbyHigherIntent = 0;
        int nearbyMissing = 0;
        string? keepPlaybackWorkerVideoPath = null;
        string candidateWindowSummary;
        lock (_activeWorkers)
        {
            currentOutcome = IntentApplyOutcome.MissingTask;
            int start = Math.Max(0, currentIndex - 1);
            int end = Math.Min(orderedVideoPaths.Count - 1, currentIndex + Math.Max(0, lookaheadCount));
            List<string> candidateWindow = [];
            for (int i = start; i <= end; i++)
            {
                string videoPath = orderedVideoPaths[i];
                if (!_taskStore.TryGetTask(videoPath, out var task))
                {
                    nearbyMissing++;
                    continue;
                }

                if (task.State == ThumbnailState.Ready)
                {
                    nearbyReady++;
                    continue;
                }

                candidateWindow.Add($"{i}:{Path.GetFileName(videoPath)}{(i == currentIndex ? "*" : "")}");
                keepPlaybackWorkerVideoPath ??= videoPath;

                IntentApplyOutcome outcome = i == currentIndex
                    ? _taskStore.ApplyIntentToVideo(videoPath, ThumbnailWorkIntent.PlaybackCurrent, task.SourceCollectionId, updatedAtTicks)
                    : _taskStore.ApplyIntentToVideo(videoPath, ThumbnailWorkIntent.PlaybackNearby, task.SourceCollectionId, updatedAtTicks);

                if (i == currentIndex)
                    currentOutcome = outcome;

                switch (outcome)
                {
                    case IntentApplyOutcome.Applied:
                        nearbyApplied++;
                        break;
                    case IntentApplyOutcome.AlreadyReady:
                        nearbyReady++;
                        break;
                    case IntentApplyOutcome.HigherIntentAlreadyPresent:
                        nearbyHigherIntent++;
                        break;
                    default:
                        nearbyMissing++;
                        break;
                }
            }

            if (currentOutcome == IntentApplyOutcome.MissingTask &&
                _taskStore.TryGetTask(currentVideoPath, out var currentTask) &&
                currentTask.State == ThumbnailState.Ready)
            {
                currentOutcome = IntentApplyOutcome.AlreadyReady;
            }

            _taskStore.CurrentForegroundTargetVideoPath = currentVideoPath;
            _taskStore.CurrentForegroundTargetIntent = ThumbnailWorkIntent.PlaybackCurrent.ToString();
            candidateWindowSummary = candidateWindow.Count == 0 ? "-" : string.Join(", ", candidateWindow);
            stalePlaybackWorkers = CountStalePlaybackWorkersUnsafe(currentVideoPath, keepPlaybackWorkerVideoPath);

            shouldPreemptLowerPriority = stalePlaybackWorkers == 0 &&
                ShouldPreemptActiveWorkersUnsafe(ThumbnailWorkIntent.PlaybackCurrent);
        }

        Log.Info(
            $"Thumbnail playback window boost: currentIndex={currentIndex}, lookahead={lookaheadCount}, currentFile={Path.GetFileName(currentVideoPath)}, keepFile={Path.GetFileName(keepPlaybackWorkerVideoPath ?? string.Empty)}, " +
            $"candidateWindow=[{candidateWindowSummary}], currentOutcome={currentOutcome}, nearbyApplied={nearbyApplied}, nearbyReady={nearbyReady}, nearbyHigherIntent={nearbyHigherIntent}, nearbyMissing={nearbyMissing}, shouldPreemptLowerPriority={shouldPreemptLowerPriority}, stalePlaybackWorkers={stalePlaybackWorkers}");
        if (stalePlaybackWorkers > 0)
            PreemptStalePlaybackWorkers(currentVideoPath, keepPlaybackWorkerVideoPath);
        else if (shouldPreemptLowerPriority)
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

        lock (_activeWorkers)
        {
            changed = _isPlayerActive != isActive;
            _isPlayerActive = isActive;

            if (!isActive)
            {
                demotedPlaybackTasks = DemotePlaybackIntentsUnsafe();
                ClearPlaybackForegroundTargetUnsafe();

                playbackWorkersToCancel = _activeWorkers
                    .Where(static worker => !worker.Execution.IsCompleted)
                    .Where(worker => IsPlaybackIntent(worker.Task.Intent))
                    .ToList();

                foreach (var worker in playbackWorkersToCancel)
                    worker.CancellationReason = "player-inactive";

                playbackWorkerFiles = string.Join(", ",
                    playbackWorkersToCancel.Select(worker => Path.GetFileName(worker.Task.VideoPath)));
            }

            snapshot = BuildSchedulerSnapshotUnsafe();
        }

        if (!changed && demotedPlaybackTasks == 0 && playbackWorkersToCancel.Count == 0)
            return;

        Log.Info(
            $"Thumbnail player activity changed: isActive={isActive}, demotedPlaybackTasks={demotedPlaybackTasks}, " +
            $"cancelPlaybackWorkers={playbackWorkersToCancel.Count}, files=[{playbackWorkerFiles}], {snapshot}");

        foreach (var worker in playbackWorkersToCancel)
        {
            try
            {
                worker.Cancellation.Cancel();
            }
            catch
            {
            }
        }

        NotifyStatusChanged();
        EnsureLoopRunning();
    }

    public void RefreshPerformanceMode()
    {
        ThumbnailPerformanceMode mode = _settings.GetThumbnailPerformanceMode();
        string snapshot;
        bool changed;

        lock (_activeWorkers)
        {
            changed = _performanceMode != mode;
            _performanceMode = mode;
            snapshot = BuildSchedulerSnapshotUnsafe();
        }

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

        lock (_activeWorkers)
        {
            changed = _isGenerationPaused != paused;
            _isGenerationPaused = paused;
            snapshot = BuildSchedulerSnapshotUnsafe();
        }

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

        string snapshot;
        lock (_activeWorkers)
        {
            snapshot = BuildSchedulerSnapshotUnsafe();
        }

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

    private ThumbnailTask? DequeueNext()
    {
        lock (_activeWorkers)
        {
            if (!CanStartMoreWorkersUnsafe())
                return null;

            return _taskStore.SnapshotTasks()
                .Where(static task => task.State == ThumbnailState.Pending)
                .OrderByDescending(static task => GetIntentRank(task.Intent))
                .ThenByDescending(static task => task.IntentUpdatedAtUtcTicks)
                .ThenBy(static task => task.VideoPath, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
    }

    private async Task ProcessQueueLoop(CancellationToken ct)
    {
        await _initTcs.Task;
        while (!ct.IsCancellationRequested && !_isShuttingDown)
        {
            DrainCompletedWorkers();

            bool hasPending;
            bool canStartWorkers;
            lock (_activeWorkers)
            {
                hasPending = _taskStore.CountTasksByState(ThumbnailState.Pending) > 0;
                canStartWorkers = CanStartMoreWorkersUnsafe();
            }

            var task = DequeueNext();
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

    private async Task GenerateForTask(ThumbnailTask task, CancellationToken ct)
    {
        SetTaskState(task, ThumbnailState.Generating);
        SaveIndex();
        string startSnapshot;
        lock (_activeWorkers)
        {
            startSnapshot = BuildSchedulerSnapshotUnsafe();
        }
        Log.Info($"Thumbnail task generating: file={Path.GetFileName(task.VideoPath)}, {startSnapshot}");

        try
        {
            var result = await GenerateWithStrategyFallback(task, ct);

            if (result.State == ThumbnailState.Ready)
            {
                lock (_activeWorkers)
                {
                    task.TotalFrames = result.FrameCount;
                    SetTaskState(task, ThumbnailState.Ready);
                }
                VideoProgress?.Invoke(task.VideoPath, 100);
                VideoReady?.Invoke(task.VideoPath);
            }
            else
            {
                SetTaskState(task, ThumbnailState.Failed);
            }
        }
        catch (OperationCanceledException)
        {
            var cancelSw = Stopwatch.StartNew();
            bool requeued = _taskStore.TryRequeueTask(task.VideoPath);
            cancelSw.Stop();
            Log.Info($"Thumbnail task canceled: file={Path.GetFileName(task.VideoPath)}, elapsed={cancelSw.ElapsedMilliseconds}ms, newState={task.State}");
            throw;
        }
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
        string snapshot;
        lock (_activeWorkers)
        {
            _activeWorkers.Add(worker);

            snapshot = BuildSchedulerSnapshotUnsafe();
        }

        Log.Info(
            $"Thumbnail worker start: file={Path.GetFileName(task.VideoPath)}, intent={task.Intent}, {snapshot}");
        NotifyStatusChanged();
    }

    private async Task RunWorkerAsync(ThumbnailGeneratorWorker worker, CancellationToken ct)
    {
        var task = worker.Task;
        try
        {
            await GenerateForTask(task, ct);
            LogWorkerCompletion(task, "completed");
        }
        catch (OperationCanceledException)
        {
            string cancelReason = string.IsNullOrWhiteSpace(worker.CancellationReason)
                ? "unspecified"
                : worker.CancellationReason;
            LogWorkerCompletion(task, $"canceled({cancelReason})");
        }
        catch (Exception ex)
        {
            Log.Error("Generate thumbnail failed", ex);
            SetTaskState(task, ThumbnailState.Failed);
            LogWorkerCompletion(task, "faulted");
        }
        finally
        {
            var finallySw = Stopwatch.StartNew();
            SaveIndex();
            UpdateProgress();
            finallySw.Stop();
            Log.Info($"Thumbnail task finalize: file={Path.GetFileName(task.VideoPath)}, elapsed={finallySw.ElapsedMilliseconds}ms, state={task.State}");
        }
    }

    private void DrainCompletedWorkers()
    {
        bool changed = false;

        lock (_activeWorkers)
        {
            for (int i = _activeWorkers.Count - 1; i >= 0; i--)
            {
                if (!_activeWorkers[i].Execution.IsCompleted)
                    continue;

                _activeWorkers[i].Cancellation.Dispose();
                _activeWorkers.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
            NotifyStatusChanged();
    }

    private int GetActiveWorkerCount()
    {
        lock (_activeWorkers)
        {
            return _activeWorkers.Count;
        }
    }

    private bool CanStartMoreWorkersUnsafe()
    {
        if (_isGenerationPaused)
            return false;

        ThumbnailExecutionPolicy policy = GetExecutionPolicyUnsafe();
        if (!policy.AllowStartNewJobs)
            return false;

        return _activeWorkers.Count < policy.MaxConcurrency;
    }

    private ThumbnailExecutionPolicy GetExecutionPolicyUnsafe()
        => ThumbnailPerformancePolicy.Create(_performanceMode, _isPlayerActive);

    private string BuildSchedulerSnapshotUnsafe()
    {
        ThumbnailExecutionPolicy policy = GetExecutionPolicyUnsafe();
        return
            $"playerActive={_isPlayerActive}, mode={_performanceMode}, paused={_isGenerationPaused}, maxConcurrency={policy.MaxConcurrency}, " +
            $"allowStartNewJobs={policy.AllowStartNewJobs}, activeWorkers={_activeWorkers.Count}, " +
            $"pendingTasks={_taskStore.CountTasksByState(ThumbnailState.Pending)}, ready={_taskStore.ReadyCount}, total={_taskStore.TotalCount}, " +
            $"foregroundPending={_taskStore.CountForegroundPending()}";
    }

    private void ReportSchedulerState(string state)
    {
        string snapshot;
        string message;

        lock (_activeWorkers)
        {
            snapshot = BuildSchedulerSnapshotUnsafe();
            message = $"{state} | {snapshot}";
            if (string.Equals(_lastSchedulerState, message, StringComparison.Ordinal))
                return;

            _lastSchedulerState = message;
        }

        Log.Info($"Thumbnail scheduler state: {message}");
    }

    private string GetBlockedSchedulerReason()
    {
        lock (_activeWorkers)
        {
            if (_isGenerationPaused)
                return "blocked-generation-paused";

            ThumbnailExecutionPolicy policy = GetExecutionPolicyUnsafe();
            if (!policy.AllowStartNewJobs)
                return _isPlayerActive
                    ? "blocked-player-active-no-new-jobs"
                    : "blocked-policy-no-new-jobs";

            if (_activeWorkers.Count >= policy.MaxConcurrency)
                return "blocked-max-concurrency";

            return "blocked-unknown";
        }
    }

    private void LogWorkerCompletion(ThumbnailTask task, string outcome)
    {
        string snapshot;
        lock (_activeWorkers)
        {
            snapshot = BuildSchedulerSnapshotUnsafe();
        }

        Log.Info(
            $"Thumbnail worker end: file={Path.GetFileName(task.VideoPath)}, outcome={outcome}, " +
            $"state={task.State}, frames={task.TotalFrames}, {snapshot}");
        NotifyStatusChanged();
    }

    private void RequeueActiveWorkers(string reason)
    {
        List<ThumbnailGeneratorWorker> workersToCancel;
        string snapshot;
        string files;

        lock (_activeWorkers)
        {
            workersToCancel = _activeWorkers
                .Where(static worker => !worker.Execution.IsCompleted)
                .ToList();

            foreach (var worker in workersToCancel)
            {
                worker.CancellationReason = reason;
            }

            snapshot = BuildSchedulerSnapshotUnsafe();
            files = string.Join(", ", workersToCancel.Select(worker => Path.GetFileName(worker.Task.VideoPath)));
        }

        if (workersToCancel.Count == 0)
            return;

        Log.Info(
            $"Thumbnail active worker requeue: reason={reason}, workers={workersToCancel.Count}, files=[{files}], {snapshot}");

        foreach (var worker in workersToCancel)
        {
            try
            {
                worker.Cancellation.Cancel();
            }
            catch
            {
            }
        }
    }

    private void PreemptLowerPriorityWorkers(ThumbnailWorkIntent incomingIntent)
    {
        List<ThumbnailGeneratorWorker> workersToCancel;
        string snapshot;
        string files;

        lock (_activeWorkers)
        {
            workersToCancel = _activeWorkers
                .Where(static worker => !worker.Execution.IsCompleted)
                .Where(worker => GetIntentRank(worker.Task.Intent) < GetIntentRank(incomingIntent))
                .ToList();

            foreach (var worker in workersToCancel)
                worker.CancellationReason = $"preempted-by-{incomingIntent}";

            snapshot = BuildSchedulerSnapshotUnsafe();
            files = string.Join(", ", workersToCancel.Select(worker => $"{Path.GetFileName(worker.Task.VideoPath)}:{worker.Task.Intent}"));
        }

        if (workersToCancel.Count == 0)
            return;

        Log.Info(
            $"Thumbnail worker preempt: incomingIntent={incomingIntent}, workers={workersToCancel.Count}, files=[{files}], {snapshot}");

        foreach (var worker in workersToCancel)
        {
            try
            {
                worker.Cancellation.Cancel();
            }
            catch
            {
            }
        }
    }

    private void PreemptStalePlaybackWorkers(string currentVideoPath, string? keepPlaybackWorkerVideoPath)
    {
        List<ThumbnailGeneratorWorker> workersToCancel;
        string snapshot;
        string files;

        lock (_activeWorkers)
        {
            workersToCancel = _activeWorkers
                .Where(static worker => !worker.Execution.IsCompleted)
                .Where(worker => IsPlaybackIntent(worker.Task.Intent))
                .Where(worker => !string.Equals(worker.Task.VideoPath, currentVideoPath, StringComparison.OrdinalIgnoreCase))
                .Where(worker => string.IsNullOrWhiteSpace(keepPlaybackWorkerVideoPath) ||
                    !string.Equals(worker.Task.VideoPath, keepPlaybackWorkerVideoPath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var worker in workersToCancel)
                worker.CancellationReason = $"stale-playback-target:{Path.GetFileName(currentVideoPath)}";

            snapshot = BuildSchedulerSnapshotUnsafe();
            files = string.Join(", ", workersToCancel.Select(worker => $"{Path.GetFileName(worker.Task.VideoPath)}:{worker.Task.Intent}"));
        }

        if (workersToCancel.Count == 0)
            return;

        Log.Info(
            $"Thumbnail stale playback worker preempt: currentFile={Path.GetFileName(currentVideoPath)}, keepFile={Path.GetFileName(keepPlaybackWorkerVideoPath ?? string.Empty)}, workers={workersToCancel.Count}, files=[{files}], {snapshot}");

        foreach (var worker in workersToCancel)
        {
            try
            {
                worker.Cancellation.Cancel();
            }
            catch
            {
            }
        }
    }

    private async Task WaitForWorkersAsync()
    {
        Task[] workers;
        lock (_activeWorkers)
        {
            workers = _activeWorkers.Select(static worker => worker.Execution).ToArray();
        }

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
        List<ThumbnailGeneratorWorker> workers;
        lock (_activeWorkers)
        {
            workers = _activeWorkers.ToList();
            foreach (var worker in workers)
            {
                worker.CancellationReason ??= "shutdown";
            }
        }

        foreach (var worker in workers)
        {
            try
            {
                worker.Cancellation.Cancel();
            }
            catch
            {
            }
        }
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

        _indexRepository.CleanupTempArtifacts();

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
        int expiryDays = _settings.GetThumbnailExpiryDays();

        if (expiryDays <= 0) return;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long threshold = now - (long)expiryDays * 86400;
        List<ThumbnailTask> expired = _taskStore.SnapshotTasks()
            .Where(t => t.MarkedForDeletionAt > 0 && t.MarkedForDeletionAt < threshold)
            .ToList();

        if (expired.Count == 0) return;

        var sw = Stopwatch.StartNew();

        foreach (var t in expired)
        {
            _indexRepository.DeleteTaskDirectory(t.Md5Dir);
        }

        _taskStore.RemoveTasks(expired);

        SaveIndex();
        UpdateProgress();
        sw.Stop();
    }


    private void LoadIndex()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var loaded = _indexRepository.Load(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            _taskStore.MergeLoadedTasks(loaded);

            sw.Stop();
            Log.Info($"Thumbnail index loaded: count={loaded.Count}, ready={_taskStore.ReadyCount}, total={_taskStore.TotalCount}");
        }
        catch (Exception ex)
        {
            Log.Error("Load tasks failed", ex);
            sw.Stop();
        }
    }

    private void SaveIndex()
    {
        try
        {
            _indexRepository.Save(_taskStore.SnapshotTasks());
        }
        catch (Exception ex)
        {
            Log.Error("Save thumbnail index failed", ex);
        }
    }


    private void UpdateProgress()
    {
        ProgressChanged?.Invoke(this, new ThumbnailProgressEventArgs
        {
            Ready = _taskStore.ReadyCount,
            Total = _taskStore.TotalCount
        });
        NotifyStatusChanged();
    }

    private void NotifyStatusChanged()
        => StatusChanged?.Invoke();


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
        lock (_activeWorkers)
        {
            _activeWorkers.Add(new ThumbnailGeneratorWorker
            {
                Task = task,
                Execution = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task,
                Cancellation = new CancellationTokenSource(),
                CancellationReason = cancellationReason
            });
        }
    }

    internal bool IsActiveWorkerCancellationRequestedForTest(string videoPath)
    {
        lock (_activeWorkers)
        {
            var worker = _activeWorkers.FirstOrDefault(activeWorker =>
                string.Equals(activeWorker.Task.VideoPath, videoPath, StringComparison.OrdinalIgnoreCase));
            return worker?.Cancellation.IsCancellationRequested ?? false;
        }
    }

    internal void SimulateCanceledActiveWorkerForTest(string videoPath)
    {
        _taskStore.TryRequeueTask(videoPath);
    }

    private void SetTaskState(ThumbnailTask task, ThumbnailState newState)
        => _taskStore.SetTaskState(task, newState);

    private async Task<RenderResult> GenerateWithStrategyFallback(ThumbnailTask task, CancellationToken ct)
    {
        IReadOnlyList<ThumbnailDecodeStrategy> strategies = _decodeStrategyService.GetStrategyChain();
        RenderResult lastResult = new(ThumbnailState.Failed);

        foreach (ThumbnailDecodeStrategy strategy in strategies)
        {
            ct.ThrowIfCancellationRequested();
            Log.Info($"Thumbnail render attempt: file={Path.GetFileName(task.VideoPath)}, strategy={strategy}");
            lastResult = await _renderer.GenerateAsync(task, strategy, ct, (p, v) => VideoProgress?.Invoke(p, v));

            if (lastResult.State == ThumbnailState.Ready)
            {
                _decodeStrategyService.RecordSuccess(strategy);
                Log.Info(
                    $"Thumbnail render success: file={Path.GetFileName(task.VideoPath)}, " +
                    $"strategy={strategy}, frames={lastResult.FrameCount}");
                return lastResult;
            }

            Log.Info($"Thumbnail render fallback: file={Path.GetFileName(task.VideoPath)}, strategy={strategy}, result={lastResult.State}");
        }

        Log.Warning(
            $"Thumbnail render failed all strategies: file={Path.GetFileName(task.VideoPath)}, " +
            $"attempts={string.Join(" -> ", strategies)}");
        return lastResult;
    }

    private static int GetIntentRank(ThumbnailWorkIntent intent)
        => intent switch
        {
            ThumbnailWorkIntent.ManualSingle => 5,
            ThumbnailWorkIntent.PlaybackCurrent => 4,
            ThumbnailWorkIntent.PlaybackNearby => 3,
            ThumbnailWorkIntent.ManualCollection => 2,
            ThumbnailWorkIntent.FocusedCollection => 1,
            _ => 0
        };

    private static bool IsPlaybackIntent(ThumbnailWorkIntent intent)
        => intent is ThumbnailWorkIntent.PlaybackCurrent or ThumbnailWorkIntent.PlaybackNearby;

    private bool ShouldPreemptActiveWorkersUnsafe(ThumbnailWorkIntent incomingIntent)
    {
        int incomingRank = GetIntentRank(incomingIntent);
        foreach (var worker in _activeWorkers)
        {
            if (worker.Execution.IsCompleted)
                continue;

            if (GetIntentRank(worker.Task.Intent) < incomingRank)
                return true;
        }

        return false;
    }

    private int CountForegroundPendingUnsafe()
        => _taskStore.CountForegroundPending();

    private int CountStalePlaybackWorkersUnsafe(string currentVideoPath, string? keepPlaybackWorkerVideoPath = null)
        => _activeWorkers.Count(worker =>
            !worker.Execution.IsCompleted &&
            IsPlaybackIntent(worker.Task.Intent) &&
            !string.Equals(worker.Task.VideoPath, currentVideoPath, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(keepPlaybackWorkerVideoPath) ||
             !string.Equals(worker.Task.VideoPath, keepPlaybackWorkerVideoPath, StringComparison.OrdinalIgnoreCase)));

    private int DemotePlaybackIntentsUnsafe()
        => _taskStore.DemotePlaybackIntents();

    private void ClearPlaybackForegroundTargetUnsafe()
        => _taskStore.ClearPlaybackForegroundTarget();

}





