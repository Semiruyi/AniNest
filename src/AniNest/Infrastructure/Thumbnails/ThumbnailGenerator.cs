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
    public int Priority { get; set; }
    public ThumbnailState State { get; set; } = ThumbnailState.Pending;
    public int TotalFrames { get; set; }
    public long MarkedForDeletionAt { get; set; }
}

public class ThumbnailProgressEventArgs : EventArgs
{
    public int Ready { get; init; }
    public int Total { get; init; }
}

public class ThumbnailGenerator : IThumbnailGenerator, IDisposable
{
    private static readonly Logger Log = AppLog.For<ThumbnailGenerator>();

    // Dependencies
    private readonly ISettingsService _settings;
    private readonly IThumbnailDecodeStrategyService _decodeStrategyService;

    // Paths
    private readonly string _thumbBaseDir;
    private readonly string _indexPath;
    private readonly string _ffmpegPath;

    // Queue & state
    private readonly List<ThumbnailTask> _tasks = new();
    private readonly object _taskLock = new();
    private readonly object _indexIoLock = new();
    private readonly Dictionary<string, ThumbnailTask> _videoToTask = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ThumbnailGeneratorWorker> _activeWorkers = new();
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private TaskCompletionSource _initTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ThumbnailRenderer _renderer;
    private bool _isShuttingDown;
    private bool _isPlayerActive;
    private int _readyCount;
    private int _totalCount;
    private ThumbnailPerformanceMode _performanceMode;
    private string? _lastSchedulerState;

    // Events
    public event EventHandler<ThumbnailProgressEventArgs>? ProgressChanged;
    public event Action<string, int>? VideoProgress; // videoPath, percent 0-100
    public event Action<string>? VideoReady; // videoPath

    // Detection
    private bool _ffmpegAvailable;

    public bool IsFfmpegAvailable => _ffmpegAvailable;

    public ThumbnailGenerator(
        ISettingsService settings,
        IThumbnailDecodeStrategyService decodeStrategyService)
    {
        _settings = settings;
        _decodeStrategyService = decodeStrategyService;
        _performanceMode = _settings.GetThumbnailPerformanceMode();
        _thumbBaseDir = AppPaths.ThumbnailDirectory;
        _indexPath = Path.Combine(_thumbBaseDir, "index.json");
        _ffmpegPath = AppPaths.FfmpegPath;
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

        CleanupTempDirs();

        LoadIndex();

        sw.Stop();

        _initTcs.TrySetResult();
        EnsureLoopRunning();
        StartExpiryCleanup();
    }


    public void EnqueueFolder(string folderPath, IReadOnlyCollection<string> videoFiles, int cardOrder,
        string? lastPlayedPath, HashSet<string> playedPaths)
    {
        if (!_ffmpegAvailable)
        {
            return;
        }

        var sw = Stopwatch.StartNew();

        int added = 0;
        foreach (var videoPath in videoFiles)
        {
            lock (_taskLock)
            {
                if (_videoToTask.TryGetValue(videoPath, out var existing))
                {
                    if (existing.MarkedForDeletionAt != 0)
                    {
                        existing.MarkedForDeletionAt = 0;
                        SaveIndex();
                    }
                    continue;
                }
            }

            int videoWeight = 2; // played
            if (string.Equals(videoPath, lastPlayedPath, StringComparison.OrdinalIgnoreCase))
                videoWeight = 0; // last played
            else if (!playedPaths.Contains(videoPath))
                videoWeight = 1; // unplayed

            int priority = cardOrder * 1000 + videoWeight;
            var task = new ThumbnailTask
            {
                VideoPath = videoPath,
                Md5Dir = ComputeMd5(videoPath),
                Priority = priority,
                State = ThumbnailState.Pending
            };

            lock (_taskLock)
            {
                _tasks.Add(task);
                _videoToTask[videoPath] = task;
                _totalCount++;
            }
            added++;
        }

        if (added > 0)
            SortQueue();
        EnsureLoopRunning();

        sw.Stop();
    }

    public void DeleteForFolder(string folderPath, IReadOnlyCollection<string>? videoFiles = null)
    {
        var sw = Stopwatch.StartNew();
        int marked = 0;

        if (videoFiles != null)
        {
            foreach (var videoPath in videoFiles)
            {
                MarkForDeletion(videoPath);
                marked++;
            }
        }

        lock (_taskLock)
        {
            var matching = _tasks.Where(t =>
                t.VideoPath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var t in matching)
            {
                if (videoFiles == null || !videoFiles.Contains(t.VideoPath, StringComparer.OrdinalIgnoreCase))
                {
                    t.MarkedForDeletionAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    marked++;
                }
            }
        }

        SaveIndex();
        sw.Stop();

        lock (_taskLock)
        {
            var markedPaths = _tasks.Where(t =>
                t.MarkedForDeletionAt > 0 &&
                t.VideoPath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase))
                .Select(t => t.VideoPath);
        }
    }

    public void SetPlayerActive(bool isActive)
    {
        bool changed;
        string snapshot;

        lock (_taskLock)
        {
            changed = _isPlayerActive != isActive;
            _isPlayerActive = isActive;
            snapshot = BuildSchedulerSnapshotUnsafe();
        }

        if (!changed)
            return;

        Log.Info($"Thumbnail player activity changed: isActive={isActive}, {snapshot}");
        EnsureLoopRunning();
    }

    public void RefreshPerformanceMode()
    {
        ThumbnailPerformanceMode mode = _settings.GetThumbnailPerformanceMode();
        string snapshot;
        bool changed;

        lock (_taskLock)
        {
            changed = _performanceMode != mode;
            _performanceMode = mode;
            snapshot = BuildSchedulerSnapshotUnsafe();
        }

        if (!changed)
            return;

        Log.Info($"Thumbnail performance mode changed: selectedMode={mode}, {snapshot}");
        RequeueActiveWorkers("performance-mode-changed");
        EnsureLoopRunning();
    }

    public void RefreshDecodeStrategy()
    {
        _decodeStrategyService.RefreshAccelerationMode();

        string snapshot;
        lock (_taskLock)
        {
            snapshot = BuildSchedulerSnapshotUnsafe();
        }

        Log.Info($"Thumbnail decode strategy refreshed: {snapshot}");
        RequeueActiveWorkers("decode-strategy-changed");
        EnsureLoopRunning();
    }

    private void MarkForDeletion(string videoPath)
    {
        lock (_taskLock)
        {
            if (_videoToTask.TryGetValue(videoPath, out var task) &&
                task.State == ThumbnailState.Ready && task.MarkedForDeletionAt == 0)
            {
                task.MarkedForDeletionAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }
    }


    public ThumbnailState GetState(string videoPath)
    {
        using var span = PerfSpan.Begin("Thumbnail.GetState", new Dictionary<string, string>
        {
            ["file"] = Path.GetFileName(videoPath)
        });
        lock (_taskLock)
        {
            if (_videoToTask.TryGetValue(videoPath, out var task))
                return task.State;
        }
        return ThumbnailState.Pending;
    }

    public string? GetThumbnailPath(string videoPath, int second)
    {
        if (!_ffmpegAvailable) return null;

        ThumbnailTask? task;
        lock (_taskLock)
        {
            _videoToTask.TryGetValue(videoPath, out task);
        }

        if (task == null || task.State != ThumbnailState.Ready) return null;

        string path = Path.Combine(_thumbBaseDir, task.Md5Dir, $"{second + 1:D4}.jpg");
        return File.Exists(path) ? path : null;
    }


    private void EnsureLoopRunning()
    {
        if (_loopTask != null && !_loopTask.IsCompleted) return;
        if (_isShuttingDown) return;

        _loopCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => ProcessQueueLoop(_loopCts.Token));
    }

    private void SortQueue()
    {
        lock (_taskLock)
        {
            _tasks.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            _tasks.Sort((a, b) =>
            {
                int aBlocked = a.State is ThumbnailState.Ready or ThumbnailState.Generating ? 1 : 0;
                int bBlocked = b.State is ThumbnailState.Ready or ThumbnailState.Generating ? 1 : 0;
                int cmp = aBlocked.CompareTo(bBlocked);
                return cmp != 0 ? cmp : a.Priority.CompareTo(b.Priority);
            });
        }
    }

    private ThumbnailTask? DequeueNext()
    {
        lock (_taskLock)
        {
            if (!CanStartMoreWorkersUnsafe())
                return null;

            foreach (var t in _tasks)
            {
                if (t.State == ThumbnailState.Pending)
                    return t;
            }
        }
        return null;
    }

    private async Task ProcessQueueLoop(CancellationToken ct)
    {
        await _initTcs.Task;
        while (!ct.IsCancellationRequested && !_isShuttingDown)
        {
            DrainCompletedWorkers();

            bool hasPending;
            bool canStartWorkers;
            lock (_taskLock)
            {
                hasPending = _tasks.Any(static t => t.State == ThumbnailState.Pending);
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
        task.State = ThumbnailState.Generating;
        SaveIndex();
        string startSnapshot;
        lock (_taskLock)
        {
            startSnapshot = BuildSchedulerSnapshotUnsafe();
        }
        Log.Info($"Thumbnail task generating: file={Path.GetFileName(task.VideoPath)}, {startSnapshot}");

        try
        {
            var result = await GenerateWithStrategyFallback(task, ct);

            if (result.State == ThumbnailState.Ready)
            {
                task.State = ThumbnailState.Ready;
                task.TotalFrames = result.FrameCount;
                lock (_taskLock) { _readyCount++; }
                VideoProgress?.Invoke(task.VideoPath, 100);
                VideoReady?.Invoke(task.VideoPath);
            }
            else
            {
                task.State = ThumbnailState.Failed;
            }
        }
        catch (OperationCanceledException)
        {
            var cancelSw = Stopwatch.StartNew();
            if (task.State == ThumbnailState.Generating)
                task.State = ThumbnailState.Pending;
            cancelSw.Stop();
            Log.Info($"Thumbnail task canceled: file={Path.GetFileName(task.VideoPath)}, elapsed={cancelSw.ElapsedMilliseconds}ms, newState={task.State}");
            throw;
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
        int activeWorkers;
        int pendingTasks;
        string snapshot;
        lock (_taskLock)
        {
            _activeWorkers.Add(worker);

            activeWorkers = _activeWorkers.Count;
            pendingTasks = CountTasksByStateUnsafe(ThumbnailState.Pending);
            snapshot = BuildSchedulerSnapshotUnsafe();
        }

        Log.Info(
            $"Thumbnail worker start: file={Path.GetFileName(task.VideoPath)}, priority={task.Priority}, {snapshot}");
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
            task.State = ThumbnailState.Failed;
            SaveIndex();
            UpdateProgress();
            LogWorkerCompletion(task, "faulted");
        }
    }

    private void DrainCompletedWorkers()
    {
        lock (_taskLock)
        {
            for (int i = _activeWorkers.Count - 1; i >= 0; i--)
            {
                if (!_activeWorkers[i].Execution.IsCompleted)
                    continue;

                _activeWorkers[i].Cancellation.Dispose();
                _activeWorkers.RemoveAt(i);
            }
        }
    }

    private int GetActiveWorkerCount()
    {
        lock (_taskLock)
        {
            return _activeWorkers.Count;
        }
    }

    private bool CanStartMoreWorkersUnsafe()
    {
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
            $"playerActive={_isPlayerActive}, mode={_performanceMode}, maxConcurrency={policy.MaxConcurrency}, " +
            $"allowStartNewJobs={policy.AllowStartNewJobs}, activeWorkers={_activeWorkers.Count}, " +
            $"pendingTasks={CountTasksByStateUnsafe(ThumbnailState.Pending)}, ready={_readyCount}, total={_totalCount}";
    }

    private void ReportSchedulerState(string state)
    {
        string snapshot;
        string message;

        lock (_taskLock)
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
        lock (_taskLock)
        {
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
        lock (_taskLock)
        {
            snapshot = BuildSchedulerSnapshotUnsafe();
        }

        Log.Info(
            $"Thumbnail worker end: file={Path.GetFileName(task.VideoPath)}, outcome={outcome}, " +
            $"state={task.State}, frames={task.TotalFrames}, {snapshot}");
    }

    private void RequeueActiveWorkers(string reason)
    {
        List<ThumbnailGeneratorWorker> workersToCancel;
        int requeued = 0;
        string snapshot;
        string files;

        lock (_taskLock)
        {
            workersToCancel = _activeWorkers
                .Where(static worker => !worker.Execution.IsCompleted)
                .ToList();

            foreach (var worker in workersToCancel)
            {
                worker.CancellationReason = reason;
                if (worker.Task.State == ThumbnailState.Generating)
                {
                    worker.Task.State = ThumbnailState.Pending;
                    requeued++;
                }
            }

            if (requeued > 0)
                SortQueue();

            snapshot = BuildSchedulerSnapshotUnsafe();
            files = string.Join(", ", workersToCancel.Select(worker => Path.GetFileName(worker.Task.VideoPath)));
        }

        if (workersToCancel.Count == 0)
            return;

        Log.Info(
            $"Thumbnail active worker requeue: reason={reason}, workers={workersToCancel.Count}, requeued={requeued}, files=[{files}], {snapshot}");

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
        lock (_taskLock)
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
        lock (_taskLock)
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

        CleanupTempDirs();

        SaveIndex();
        sw.Stop();
    }

    private void CleanupTempDirs()
    {
        try
        {
            var tmpDirs = Directory.GetDirectories(_thumbBaseDir, ".tmp_*");
            if (tmpDirs.Length > 0)
            {
                foreach (var dir in tmpDirs)
                {
                    try { Directory.Delete(dir, true); }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Cleanup temp directories failed", ex);
        }
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
        List<ThumbnailTask> expired;

        lock (_taskLock)
        {
            expired = _tasks.Where(t =>
                t.MarkedForDeletionAt > 0 && t.MarkedForDeletionAt < threshold).ToList();
        }

        if (expired.Count == 0) return;

        var sw = Stopwatch.StartNew();

        foreach (var t in expired)
        {
            string dir = Path.Combine(_thumbBaseDir, t.Md5Dir);
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch (Exception ex)
            {
                Log.Error("Delete expired thumbnail directory failed", ex);
            }

            lock (_taskLock)
            {
                _tasks.Remove(t);
                _videoToTask.Remove(t.VideoPath);
                if (t.State == ThumbnailState.Ready) _readyCount--;
                _totalCount--;
            }
        }

        SaveIndex();
        UpdateProgress();
        sw.Stop();
    }


    private void LoadIndex()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            lock (_taskLock)
            {
                foreach (var key in _videoToTask.Keys)
                    existingPaths.Add(key);
            }

            var loaded = ThumbnailIndex.Load(_indexPath, _thumbBaseDir, existingPaths);

            lock (_taskLock)
            {
                foreach (var task in loaded)
                {
                    _tasks.Add(task);
                    _videoToTask[task.VideoPath] = task;
                    _totalCount++;
                    if (task.State == ThumbnailState.Ready) _readyCount++;
                }
            }

            sw.Stop();
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
            ThumbnailTask[] snapshot;
            lock (_taskLock) { snapshot = _tasks.ToArray(); }
            lock (_indexIoLock)
            {
                ThumbnailIndex.Save(_indexPath, snapshot);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Save thumbnail index failed", ex);
        }
    }


    private void UpdateProgress()
    {
        int ready, total;
        lock (_taskLock)
        {
            ready = _readyCount;
            total = _totalCount;
        }

        ProgressChanged?.Invoke(this, new ThumbnailProgressEventArgs { Ready = ready, Total = total });
    }


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
        lock (_taskLock)
        {
            return _tasks.Count;
        }
    }

    private int CountTasksByState(ThumbnailState state)
    {
        lock (_taskLock)
        {
            return CountTasksByStateUnsafe(state);
        }
    }

    private int CountTasksByStateUnsafe(ThumbnailState state)
    {
        int count = 0;
        foreach (var task in _tasks)
        {
            if (task.State == state)
                count++;
        }

        return count;
    }

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
}





