using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
namespace AniNest.Infrastructure.Thumbnails;

public class ThumbnailGenerator : IThumbnailGenerator, IDisposable
{
    private static readonly Logger Log = AppLog.For<ThumbnailGenerator>();

    // Core state
    private readonly string _thumbBaseDir;
    private readonly string _ffmpegPath;
    private readonly ThumbnailTaskStore _taskStore = new();
    private readonly ThumbnailWorkerPool _workerPool = new();
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private TaskCompletionSource _initTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ThumbnailCacheMaintenance _cacheMaintenance;
    private readonly ThumbnailWorkerExecutionHost _workerExecutionHost;
    private readonly ThumbnailWorkerCancellationCoordinator _workerCancellationCoordinator;
    private readonly ThumbnailStatusTracker _statusTracker;
    private readonly ThumbnailQueueLoopRunner _queueLoopRunner;
    private readonly ThumbnailCollectionCoordinator _collectionCoordinator;
    private readonly ThumbnailPlaybackCoordinator _playbackCoordinator;
    private readonly ThumbnailQueryService _queryService;
    private readonly ThumbnailRuntimeController _runtimeController;
    private bool _isShuttingDown;
    private bool _isPlayerActive;
    private bool _isGenerationPaused;
    private ThumbnailPerformanceMode _performanceMode;
    private string? _lastSchedulerState;

    // External surface
    public event EventHandler<ThumbnailProgressEventArgs>? ProgressChanged;
    public event Action<string, int>? VideoProgress; // videoPath, percent 0-100
    public event Action<string>? VideoReady; // videoPath
    public event Action? StatusChanged;

    private bool _ffmpegAvailable;

    public bool IsFfmpegAvailable => _ffmpegAvailable;

    public ThumbnailGenerationStatusSnapshot GetStatusSnapshot()
        => _queryService.GetStatusSnapshot();

    public ThumbnailState GetThumbnailState(string videoPath)
        => _queryService.GetThumbnailState(videoPath);

    public ThumbnailGenerator(
        ISettingsService settings,
        IThumbnailDecodeStrategyService decodeStrategyService)
        : this(settings, decodeStrategyService, processController: null)
    {
    }

    internal ThumbnailGenerator(
        ISettingsService settings,
        IThumbnailDecodeStrategyService decodeStrategyService,
        IThumbnailProcessController? processController)
    {
        _performanceMode = settings.GetThumbnailPerformanceMode();
        _isGenerationPaused = settings.IsThumbnailGenerationPaused();
        _thumbBaseDir = AppPaths.ThumbnailDirectory;
        _ffmpegPath = AppPaths.FfmpegPath;
        var components = ThumbnailGeneratorComponents.Create(
            _thumbBaseDir,
            _ffmpegPath,
            settings,
            decodeStrategyService,
            processController,
            _taskStore,
            _workerPool,
            _initTcs.Task,
            () => _isShuttingDown,
            () => _taskStore.CountTasksByState(ThumbnailState.Pending) > 0,
            () => ThumbnailQueueScheduler.CanStartMoreWorkers(_workerPool, _isGenerationPaused, _performanceMode, _isPlayerActive),
            () => ThumbnailQueueScheduler.SelectNextTask(_taskStore, _workerPool, _isGenerationPaused, _performanceMode, _isPlayerActive),
            StartWorker,
            DrainCompletedWorkers,
            ReportSchedulerState,
            GetBlockedSchedulerReason,
            GetActiveWorkerCount,
            WaitForWorkersAsync,
            SetTaskState,
            SaveIndex,
            args => ProgressChanged?.Invoke(this, args),
            () => StatusChanged?.Invoke(),
            (path, percent) => VideoProgress?.Invoke(path, percent),
            path => VideoReady?.Invoke(path),
            BuildSchedulerSnapshot,
            GetVideoDuration);
        _cacheMaintenance = components.CacheMaintenance;
        _statusTracker = components.StatusTracker;
        _workerExecutionHost = components.WorkerExecutionHost;
        _workerCancellationCoordinator = components.WorkerCancellationCoordinator;
        _queueLoopRunner = components.QueueLoopRunner;
        _collectionCoordinator = new ThumbnailCollectionCoordinator(
            _taskStore,
            _workerPool,
            components.IndexRepository,
            EnsureLoopRunning,
            NotifyStatusChanged,
            SaveIndex,
            PreemptLowerPriorityWorkers,
            () => _ffmpegAvailable,
            ComputeMd5);
        _playbackCoordinator = new ThumbnailPlaybackCoordinator(
            _taskStore,
            _workerPool,
            EnsureLoopRunning,
            NotifyStatusChanged,
            PreemptLowerPriorityWorkers,
            PreemptStalePlaybackWorkers);
        _queryService = new ThumbnailQueryService(
            _taskStore,
            _statusTracker,
            _workerPool,
            _thumbBaseDir,
            () => _isGenerationPaused,
            () => _isPlayerActive);
        _runtimeController = new ThumbnailRuntimeController(
            settings,
            decodeStrategyService,
            _workerPool,
            _workerCancellationCoordinator,
            components.WorkerSuspensionCoordinator,
            _cacheMaintenance,
            _statusTracker,
            EnsureLoopRunning,
            NotifyStatusChanged,
            SaveIndex,
            SetTaskState,
            BuildSchedulerSnapshot,
            () => _isPlayerActive,
            value => _isPlayerActive = value,
            () => _performanceMode,
            mode => _performanceMode = mode,
            () => _isGenerationPaused,
            paused => _isGenerationPaused = paused,
            DemotePlaybackIntents,
            ClearPlaybackForegroundTarget);

        Directory.CreateDirectory(_thumbBaseDir);

        Task.Run(Initialize);
    }

    // Initialization
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

    // Public facade
    public void RegisterCollection(LibraryCollectionRef collection, IReadOnlyCollection<string> videoPaths)
        => _collectionCoordinator.RegisterCollection(collection, videoPaths);

    public void RemoveCollection(string collectionId)
        => _collectionCoordinator.RemoveCollection(collectionId);

    public void DeleteCollection(string collectionId, IReadOnlyCollection<string>? videoPaths = null)
        => _collectionCoordinator.DeleteCollection(collectionId, videoPaths);

    public void FocusCollection(string collectionId)
        => _collectionCoordinator.FocusCollection(collectionId);

    public void BoostCollection(string collectionId)
        => _collectionCoordinator.BoostCollection(collectionId);

    public void BoostVideo(string videoPath)
        => _playbackCoordinator.BoostVideo(videoPath);

    public void BoostPlaybackWindow(IReadOnlyList<string> orderedVideoPaths, int currentIndex, int lookaheadCount)
        => _playbackCoordinator.BoostPlaybackWindow(orderedVideoPaths, currentIndex, lookaheadCount);

    public void ResetCollection(string collectionId, bool boostAfterReset)
        => _collectionCoordinator.ResetCollection(collectionId, boostAfterReset);

    public void SetPlayerActive(bool isActive)
        => _runtimeController.SetPlayerActive(isActive);

    public void RefreshPerformanceMode()
        => _runtimeController.RefreshPerformanceMode();

    public void RefreshGenerationPaused()
        => _runtimeController.RefreshGenerationPaused();

    public void RefreshDecodeStrategy()
        => _runtimeController.RefreshDecodeStrategy();

    public byte[]? GetThumbnailBytes(string videoPath, long positionMs)
        => _queryService.GetThumbnailBytes(videoPath, positionMs, _ffmpegAvailable);

    // Scheduler and worker orchestration
    private void EnsureLoopRunning()
    {
        if (_loopTask != null && !_loopTask.IsCompleted) return;
        if (_isShuttingDown) return;

        _loopCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => _queueLoopRunner.RunAsync(_loopCts.Token));
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
        string snapshot = BuildSchedulerSnapshot();

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

    private string BuildSchedulerSnapshot()
        => ThumbnailQueueScheduler.BuildSnapshot(_workerPool, _taskStore, _isGenerationPaused, _isPlayerActive, _performanceMode);

    private void ReportSchedulerState(string state)
    {
        string snapshot;
        string message;

        snapshot = BuildSchedulerSnapshot();
        message = $"{state} | {snapshot}";
        if (string.Equals(_lastSchedulerState, message, StringComparison.Ordinal))
            return;

        _lastSchedulerState = message;

        Log.Info($"Thumbnail scheduler state: {message}");
    }

    private string GetBlockedSchedulerReason()
        => ThumbnailQueueScheduler.GetBlockedReason(_workerPool, _isGenerationPaused, _performanceMode, _isPlayerActive);

    private void RequeueActiveWorkers(string reason)
        => _runtimeController.RequeueActiveWorkers(reason);

    private void PauseActiveWorkers()
        => _runtimeController.PauseActiveWorkers();

    private void ResumePausedWorkers()
        => _runtimeController.ResumePausedWorkers();

    private void PreemptLowerPriorityWorkers(ThumbnailWorkIntent incomingIntent, string? protectedVideoPath = null)
    {
        List<ThumbnailGeneratorWorker> workersToCancel;
        workersToCancel = _workerPool.MarkForCancellation(
            workers => ThumbnailWorkerPreemption.SelectLowerPriorityWorkers(workers, incomingIntent, protectedVideoPath),
            worker => $"preempted-by-{incomingIntent}");
        _workerCancellationCoordinator.CancelWithComputedReasons(
            workersToCancel,
            worker => $"preempted-by-{incomingIntent}",
            "Thumbnail worker preempt");
    }

    private void PreemptStalePlaybackWorkers(string currentVideoPath, string? keepPlaybackWorkerVideoPath, IReadOnlyCollection<string> stalePlaybackWorkerVideoPaths)
    {
        List<ThumbnailGeneratorWorker> workersToCancel;
        workersToCancel = _workerPool.MarkForCancellation(
            workers => ThumbnailWorkerPreemption.SelectStalePlaybackWorkers(
                workers,
                stalePlaybackWorkerVideoPaths),
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

    public void Shutdown()
        => _runtimeController.Shutdown(_loopCts, _loopTask, _expiryCts, () => _isShuttingDown = true);

    // Background maintenance
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
        => _runtimeController.CleanupExpired(_taskStore);


    private void LoadIndex()
        => _cacheMaintenance.LoadInto(_taskStore);

    private void SaveIndex()
        => _cacheMaintenance.SaveFrom(_taskStore);

    private void NotifyStatusChanged()
        => _statusTracker.NotifyStatusChanged();

    // Shared helpers
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

    // Test hooks
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

    internal void PreemptLowerPriorityWorkersForTest(ThumbnailWorkIntent incomingIntent, string? protectedVideoPath = null)
        => PreemptLowerPriorityWorkers(incomingIntent, protectedVideoPath);

    internal void AddActiveWorkerForTest(string videoPath, string cancellationReason = "test-worker")
    {
        if (!_taskStore.TryGetTask(videoPath, out var task))
            return;

        task.State = ThumbnailState.Generating;
        _workerPool.AddTestWorker(task, cancellationReason);
    }

    internal void AddActiveWorkerForTest(string videoPath, int processId, string cancellationReason = "test-worker")
    {
        if (!_taskStore.TryGetTask(videoPath, out var task))
            return;

        task.State = ThumbnailState.Generating;
        _workerPool.AddTestWorker(task, cancellationReason, processId);
    }

    internal bool IsActiveWorkerCancellationRequestedForTest(string videoPath)
        => _workerPool.IsCancellationRequested(videoPath);

    internal bool IsActiveWorkerSuspendedForTest(string videoPath)
        => _workerPool.IsSuspended(videoPath);

    internal void SimulateCanceledActiveWorkerForTest(string videoPath)
    {
        _taskStore.TryRequeueTask(videoPath);
    }

    private void SetTaskState(ThumbnailTask task, ThumbnailState newState)
        => _taskStore.SetTaskState(task, newState);

    private int CountForegroundPending()
        => _taskStore.CountForegroundPending();

    private int DemotePlaybackIntents()
        => _taskStore.DemotePlaybackIntents();

    private void ClearPlaybackForegroundTarget()
        => _taskStore.ClearPlaybackForegroundTarget();
}





