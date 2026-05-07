using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LocalPlayer.Infrastructure.Diagnostics;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Interop;
namespace LocalPlayer.Infrastructure.Thumbnails;

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

    // Paths
    private readonly string _thumbBaseDir;
    private readonly string _indexPath;
    private readonly string _ffmpegPath;

    // Queue & state
    private readonly List<ThumbnailTask> _tasks = new();
    private readonly object _taskLock = new();
    private readonly Dictionary<string, ThumbnailTask> _videoToTask = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private TaskCompletionSource _initTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ThumbnailRenderer _renderer;
    private bool _isShuttingDown;
    private int _readyCount;
    private int _totalCount;

    // Events
    public event EventHandler<ThumbnailProgressEventArgs>? ProgressChanged;
    public event Action<string, int>? VideoProgress; // videoPath, percent 0-100
    public event Action<string>? VideoReady; // videoPath

    // Detection
    private bool _ffmpegAvailable;

    public bool IsFfmpegAvailable => _ffmpegAvailable;

    public ThumbnailGenerator(ISettingsService settings)
    {
        _settings = settings;
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
            foreach (var p in markedPaths)
                Log.Info($"[DeleteForFolder] marked: {p}");
        }
    }

    private void MarkForDeletion(string videoPath)
    {
        lock (_taskLock)
        {
            if (_videoToTask.TryGetValue(videoPath, out var task) &&
                task.State == ThumbnailState.Ready && task.MarkedForDeletionAt == 0)
            {
                task.MarkedForDeletionAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Log.Info($"[MarkForDeletion] {videoPath}");
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
        Log.Info("[ProcessLoop] waiting for initialization...");
        await _initTcs.Task;
        Log.Info("[ProcessLoop] initialization completed, processing queue");
        while (!ct.IsCancellationRequested && !_isShuttingDown)
        {
            var task = DequeueNext();
            if (task == null)
            {
                try { await Task.Delay(2000, ct); } catch { break; }
                continue;
            }

            try
            {
                await GenerateForTask(task, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error("Generate thumbnail failed", ex);
                task.State = ThumbnailState.Failed;
                SaveIndex();
                UpdateProgress();
            }
        }
    }

    private async Task GenerateForTask(ThumbnailTask task, CancellationToken ct)
    {
        task.State = ThumbnailState.Generating;
        SaveIndex();

        try
        {
            var result = await _renderer.GenerateAsync(task, ct, (p, v) => VideoProgress?.Invoke(p, v));

            if (result.State == ThumbnailState.Ready)
            {
                task.State = ThumbnailState.Ready;
                task.TotalFrames = result.FrameCount;
                lock (_taskLock) { _readyCount++; }
                Log.Debug($"VideoProgress(100%) + VideoReady: {Path.GetFileName(task.VideoPath)}, subscribed={VideoProgress != null}");
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
            task.State = ThumbnailState.Pending;
            cancelSw.Stop();
            Log.Info($"[GenerateForTask] canceled in {cancelSw.ElapsedMilliseconds}ms, will retry");
            throw;
        }
        finally
        {
            var finallySw = Stopwatch.StartNew();
            SaveIndex();
            UpdateProgress();
            finallySw.Stop();
            Log.Info($"[GenerateForTask] finally completed in {finallySw.ElapsedMilliseconds}ms");
        }
    }


    public void Shutdown()
    {
        var sw = Stopwatch.StartNew();
        Log.Info("[Shutdown] starting");

        _isShuttingDown = true;

        _loopCts?.Cancel();
        _expiryCts?.Cancel();

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
            ThumbnailIndex.Save(_indexPath, snapshot);
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

        Log.Info($"[Progress] {ready}/{total}");

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
}





