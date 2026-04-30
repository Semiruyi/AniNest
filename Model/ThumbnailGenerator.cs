using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LocalPlayer.Model;

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
    public long MarkedForDeletionAt { get; set; } // Unix 时间戳，0 = 未标记
}

public class ThumbnailProgressEventArgs : EventArgs
{
    public int Ready { get; init; }
    public int Total { get; init; }
}

public class ThumbnailGenerator : IThumbnailGenerator, IDisposable
{
    private static void Log(string message) => AppLog.Info(nameof(ThumbnailGenerator), message);
    private static void LogError(string message, Exception? ex = null) => AppLog.Error(nameof(ThumbnailGenerator), message, ex);

    // Singleton
    private static readonly Lazy<ThumbnailGenerator> _instance = new(() => new ThumbnailGenerator());
    public static ThumbnailGenerator Instance => _instance.Value;

    // Dependencies
    private Func<int>? _getExpiryDays;

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

    private ThumbnailGenerator()
    {
        _thumbBaseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "thumbnails");
        _indexPath = Path.Combine(_thumbBaseDir, "index.json");
        _ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
        _renderer = new ThumbnailRenderer(_ffmpegPath, _thumbBaseDir);

        Directory.CreateDirectory(_thumbBaseDir);
        Log($"初始化: thumbBaseDir={_thumbBaseDir}");
    }

    /// <summary>
    /// 启动时调用：加载索引、清理残留、检测 ffmpeg。
    /// </summary>
    public void Initialize(Func<int> getExpiryDays)
    {
        _getExpiryDays = getExpiryDays;

        var sw = Stopwatch.StartNew();
        Log("[Initialize] 开始初始化");

        // 检测 ffmpeg
        _ffmpegAvailable = File.Exists(_ffmpegPath);
        if (!_ffmpegAvailable)
        {
            Log($"[Initialize] ffmpeg 不可用，未找到: {_ffmpegPath}，缩略图功能将不工作");
        }
        else
        {
            // 获取 ffmpeg 版本
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
                    Log($"[Initialize] ffmpeg 可用: {verOutput}");
                    versionProc.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log($"[Initialize] ffmpeg 版本检测异常: {ex.Message}");
            }
        }

        // 清理残留的 tmp 目录
        CleanupTempDirs();

        // 加载索引
        LoadIndex();

        sw.Stop();
        Log($"[Initialize] 初始化完成, 就绪 {_readyCount}/{_totalCount}, 总耗时 {sw.ElapsedMilliseconds}ms");

        // 信号：初始化完成，允许队列开始处理
        _initTcs.TrySetResult();
        EnsureLoopRunning();
        StartExpiryCleanup();
        Log("[Initialize] 已触发队列处理 + 过期清理");
    }

    // ========== 入队 ==========

    /// <summary>
    /// 批量入队文件夹中的所有视频，按优先级排序。
    /// cardOrder 越小越前面；同一卡片内 lastPlayed > unplayed > played。
    /// </summary>
    public void EnqueueFolder(string folderPath, int cardOrder,
        string? lastPlayedPath, HashSet<string> playedPaths)
    {
        if (!_ffmpegAvailable)
        {
            Log($"[EnqueueFolder] ffmpeg 不可用，跳过: {Path.GetFileName(folderPath)}");
            return;
        }

        var sw = Stopwatch.StartNew();
        var videoFiles = VideoScanner.GetVideoFiles(folderPath);
        Log($"[EnqueueFolder] {Path.GetFileName(folderPath)}: {videoFiles.Length} 个视频, cardOrder={cardOrder}");

        int added = 0;
        foreach (var videoPath in videoFiles)
        {
            // 去重 + 清除待删除标记（重新添加的卡片）
            lock (_taskLock)
            {
                if (_videoToTask.TryGetValue(videoPath, out var existing))
                {
                    if (existing.MarkedForDeletionAt != 0)
                    {
                        existing.MarkedForDeletionAt = 0;
                        SaveIndex();
                        Log($"[EnqueueFolder] 清除待删除标记: {Path.GetFileName(videoPath)}");
                    }
                    continue;
                }
            }

            // 计算优先级
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
        EnsureLoopRunning(); // 总是确保循环在运行，即使没有新任务

        sw.Stop();
        Log($"[EnqueueFolder] {Path.GetFileName(folderPath)}: 新增 {added} 个任务, 总耗时 {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// 删除文件夹时标记缩略图为待删除，而非立即删除。
    /// </summary>
    public void DeleteForFolder(string folderPath)
    {
        var sw = Stopwatch.StartNew();
        var videoFiles = VideoScanner.GetVideoFiles(folderPath);
        int marked = 0;

        foreach (var videoPath in videoFiles)
        {
            MarkForDeletion(videoPath);
            marked++;
        }

        // 文件夹已不存在时通过映射反查
        lock (_taskLock)
        {
            var matching = _tasks.Where(t =>
                t.VideoPath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var t in matching)
            {
                if (!videoFiles.Contains(t.VideoPath, StringComparer.OrdinalIgnoreCase))
                {
                    t.MarkedForDeletionAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    marked++;
                }
            }
        }

        SaveIndex();
        sw.Stop();
        Log($"[DeleteForFolder] {folderPath}: 标记待删除 {marked} 个, 总耗时 {sw.ElapsedMilliseconds}ms");

        // 列出所有被标记的视频路径
        lock (_taskLock)
        {
            var markedPaths = _tasks.Where(t =>
                t.MarkedForDeletionAt > 0 &&
                t.VideoPath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase))
                .Select(t => t.VideoPath);
            foreach (var p in markedPaths)
                Log($"[DeleteForFolder] 已标记: {p}");
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
                Log($"[MarkForDeletion] {videoPath}");
            }
        }
    }

    // ========== 查询 ==========

    public ThumbnailState GetState(string videoPath)
    {
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

        // 文件编号从 0001 开始 (ffmpeg %04d)
        string path = Path.Combine(_thumbBaseDir, task.Md5Dir, $"{second + 1:D4}.jpg");
        return File.Exists(path) ? path : null;
    }

    // ========== 队列处理 ==========

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
            // 把 Ready 和 Generating 排到末尾（不参与调度）
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
        Log("[ProcessLoop] 排队等待初始化完成...");
        await _initTcs.Task;
        Log("[ProcessLoop] 初始化已完成, 开始处理队列");
        while (!ct.IsCancellationRequested && !_isShuttingDown)
        {
            var task = DequeueNext();
            if (task == null)
            {
                // 队列空，等待后重试
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
                Log($"[ProcessLoop] 生成异常: {ex.GetType().Name}: {ex.Message}");
                task.State = ThumbnailState.Failed;
                SaveIndex();
                UpdateProgress();
            }
        }
        Log("[ProcessLoop] 队列处理循环结束");
    }

    private async Task GenerateForTask(ThumbnailTask task, CancellationToken ct)
    {
        task.State = ThumbnailState.Generating;
        SaveIndex();

        try
        {
            var result = await _renderer.GenerateAsync(task, ct, VideoProgress);

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
            // Renderer 已杀进程 + 清 tmp，这里重置状态以便下次重试
            var cancelSw = Stopwatch.StartNew();
            task.State = ThumbnailState.Pending;
            cancelSw.Stop();
            Log($"[GenerateForTask] 取消处理完成, 耗时 {cancelSw.ElapsedMilliseconds}ms, 即将重抛");
            throw;
        }
        finally
        {
            var finallySw = Stopwatch.StartNew();
            SaveIndex();
            UpdateProgress();
            finallySw.Stop();
            Log($"[GenerateForTask] finally 完成, SaveIndex+UpdateProgress 耗时 {finallySw.ElapsedMilliseconds}ms");
        }
    }

    // ========== 安全关闭 ==========

    public void Shutdown()
    {
        var sw = Stopwatch.StartNew();
        Log("[Shutdown] 开始安全关闭");

        _isShuttingDown = true;

        // 取消循环 + 过期清理 → Renderer 内部杀 ffmpeg + 清 tmp
        Log("[Shutdown] 取消 _loopCts...");
        _loopCts?.Cancel();
        Log("[Shutdown] 取消 _expiryCts...");
        _expiryCts?.Cancel();

        if (_loopTask != null)
        {
            Log($"[Shutdown] 等待 _loopTask 退出 (Status={_loopTask.Status})...");
            var waitSw = Stopwatch.StartNew();
            try { _loopTask.Wait(5000); } catch { }
            waitSw.Stop();
            Log($"[Shutdown] _loopTask 等待完成, 实际耗时 {waitSw.ElapsedMilliseconds}ms, Status={_loopTask.Status}");
        }

        // 兜底清理残留 tmp 目录
        CleanupTempDirs();

        SaveIndex();
        sw.Stop();
        Log($"[Shutdown] 安全关闭完成, 总耗时 {sw.ElapsedMilliseconds}ms");
    }

    private void CleanupTempDirs()
    {
        try
        {
            var tmpDirs = Directory.GetDirectories(_thumbBaseDir, ".tmp_*");
            if (tmpDirs.Length > 0)
            {
                Log($"[CleanupTemp] 清理 {tmpDirs.Length} 个残留 tmp 目录");
                foreach (var dir in tmpDirs)
                {
                    try { Directory.Delete(dir, true); }
                    catch (Exception ex) { Log($"[CleanupTemp] 删除失败: {dir}, {ex.Message}"); }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[CleanupTemp] 清理异常: {ex.Message}");
        }
    }

    // ========== 过期清理 ==========

    private CancellationTokenSource? _expiryCts;

    private void StartExpiryCleanup()
    {
        _expiryCts = new CancellationTokenSource();
        _ = ExpiryCleanupLoop(_expiryCts.Token);
    }

    private async Task ExpiryCleanupLoop(CancellationToken ct)
    {
        Log("[ExpiryCleanup] 启动过期清理循环");
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromHours(1), ct); } catch { break; }
            CleanupExpired();
        }
        Log("[ExpiryCleanup] 过期清理循环结束");
    }

    private void CleanupExpired()
    {
        int expiryDays = _getExpiryDays?.Invoke() ?? 30;

        if (expiryDays <= 0) return; // 永不过期

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
        Log($"[ExpiryCleanup] 发现 {expired.Count} 个过期缩略图 (过期天数={expiryDays})");

        foreach (var t in expired)
        {
            Log($"[ExpiryCleanup] 删除: 视频={t.VideoPath}, 目录={t.Md5Dir}");
            string dir = Path.Combine(_thumbBaseDir, t.Md5Dir);
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
                Log($"[ExpiryCleanup] 已删除目录: {dir}");
            }
            catch (Exception ex)
            {
                Log($"[ExpiryCleanup] 删除目录失败: {dir}, {ex.Message}");
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
        Log($"[ExpiryCleanup] 清理完成, 删除 {expired.Count} 个, 耗时 {sw.ElapsedMilliseconds}ms");
    }

    // ========== 索引持久化 ==========

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
            Log($"[LoadIndex] 加载完成, {_totalCount} 个条目 ({_readyCount} 就绪), 耗时 {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log($"[LoadIndex] 加载异常: {ex.Message}, 耗时 {sw.ElapsedMilliseconds}ms");
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
            Log($"[SaveIndex] 保存异常: {ex.Message}");
        }
    }

    // ========== 进度 ==========

    private void UpdateProgress()
    {
        int ready, total;
        lock (_taskLock)
        {
            ready = _readyCount;
            total = _totalCount;
        }

        Log($"[Progress] {ready}/{total}");

        ProgressChanged?.Invoke(this, new ThumbnailProgressEventArgs { Ready = ready, Total = total });
    }

    // ========== 工具 ==========

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
