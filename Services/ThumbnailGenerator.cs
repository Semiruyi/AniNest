using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LocalPlayer.Services;

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

internal class ThumbnailEntryDto
{
    public string Md5 { get; set; } = "";
    public string State { get; set; } = "Pending";
    public int TotalFrames { get; set; }
    public long MarkedForDeletionAt { get; set; } // 0 = 未标记
}

public class ThumbnailGenerator : IDisposable
{
    private static void Log(string message) => AppLog.Info(nameof(ThumbnailGenerator), message);
    private static void LogError(string message, Exception? ex = null) => AppLog.Error(nameof(ThumbnailGenerator), message, ex);

    // Singleton
    private static readonly Lazy<ThumbnailGenerator> _instance = new(() => new ThumbnailGenerator());
    public static ThumbnailGenerator Instance => _instance.Value;

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
    private Process? _currentProcess;
    private ThumbnailTask? _currentTask;
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

        Directory.CreateDirectory(_thumbBaseDir);
        Log($"初始化: thumbBaseDir={_thumbBaseDir}");
    }

    /// <summary>
    /// 启动时调用：加载索引、清理残留、检测 ffmpeg。
    /// </summary>
    public void Initialize()
    {
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
        var sw = Stopwatch.StartNew();

        // 标记 Generating
        task.State = ThumbnailState.Generating;
        SaveIndex();

        string tmpDir = Path.Combine(_thumbBaseDir, $".tmp_{task.Md5Dir}");
        string finalDir = Path.Combine(_thumbBaseDir, task.Md5Dir);

        // 清理可能残留的 tmp 目录
        try { if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true); } catch { }

        // 确保视频文件存在
        if (!File.Exists(task.VideoPath))
        {
            Log($"[Generate] 视频文件不存在，标记 Failed: {task.VideoPath}");
            task.State = ThumbnailState.Failed;
            SaveIndex();
            UpdateProgress();
            return;
        }

        Directory.CreateDirectory(tmpDir);
        Log($"[Generate] 开始: {Path.GetFileName(task.VideoPath)}, tmpDir={tmpDir}");

        // 获取视频总时长用于计算百分比
        double totalSec = GetVideoDuration(task.VideoPath);
        Log($"[Generate] 视频时长: {totalSec:F1}s");

        // ffmpeg 命令
        string args = $"-y -i \"{task.VideoPath}\" " +
            "-vf \"fps=1,scale='min(300,iw)':'min(300,ih)':force_original_aspect_ratio=decrease\" " +
            $"-q:v 5 \"{tmpDir}\\%04d.jpg\"";

        Log($"[Generate] ffmpeg: {_ffmpegPath} {args}");

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };

        _currentProcess = new Process { StartInfo = psi };
        _currentTask = task;
        int lastPercent = -1;

        try
        {
            ct.ThrowIfCancellationRequested();

            _currentProcess.Start();
            Log($"[Generate] ffmpeg 进程已启动, PID={_currentProcess.Id}");

            // 读取 stderr，解析 time= 计算百分比，每 1% 变化触发 VideoProgress
            var stderrTask = Task.Run(() =>
            {
                try
                {
                    string? line;
                    while ((line = _currentProcess.StandardError.ReadLine()) != null)
                    {
                        if (totalSec > 0)
                        {
                            int ti = line.IndexOf("time=", StringComparison.Ordinal);
                            if (ti >= 0)
                            {
                                int end = line.IndexOf(' ', ti + 5);
                                string timeStr = end > ti ? line.Substring(ti + 5, end - ti - 5).Trim() : line.Substring(ti + 5).Trim();
                                if (TimeSpan.TryParse(timeStr, out var ts))
                                {
                                    int percent = (int)(ts.TotalSeconds / totalSec * 100);
                                    if (percent > lastPercent && percent <= 100)
                                    {
                                        lastPercent = percent;
                                        VideoProgress?.Invoke(task.VideoPath, percent);
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }, ct);

            await _currentProcess.WaitForExitAsync(ct);
            await stderrTask;

            int exitCode = _currentProcess.ExitCode;
            sw.Stop();
            Log($"[Generate] ffmpeg 退出, ExitCode={exitCode}, 耗时 {sw.ElapsedMilliseconds / 1000.0:F1}s");

            if (exitCode == 0)
            {
                // 统计生成文件数
                int frameCount = 0;
                if (Directory.Exists(tmpDir))
                {
                    frameCount = Directory.GetFiles(tmpDir, "*.jpg").Length;
                }

                // 重命名 tmp 目录为正式目录
                if (Directory.Exists(finalDir))
                {
                    Directory.Delete(finalDir, recursive: true);
                }
                Directory.Move(tmpDir, finalDir);

                task.State = ThumbnailState.Ready;
                task.TotalFrames = frameCount;

                lock (_taskLock) { _readyCount++; }

                Log($"[Generate] 完成: {task.VideoPath}, {frameCount} 帧, 耗时 {sw.ElapsedMilliseconds / 1000.0:F1}s");
                Log($"[Generate] 目录: {finalDir}");

                // 100% 进度 + 就绪通知
                VideoProgress?.Invoke(task.VideoPath, 100);
                VideoReady?.Invoke(task.VideoPath);
            }
            else
            {
                task.State = ThumbnailState.Failed;
                Log($"[Generate] 失败: {Path.GetFileName(task.VideoPath)}, ExitCode={exitCode}");

                // ffmpeg 失败时保留 stderr 的最后几行作为日志
                try
                {
                    var errOutput = _currentProcess.StandardError.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(errOutput))
                    {
                        var lastLines = errOutput.Split('\n').TakeLast(5);
                        foreach (var line in lastLines)
                            Log($"[Generate] ffmpeg stderr: {line.Trim()}");
                    }
                }
                catch { }
            }
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            Log($"[Generate] 被取消: {Path.GetFileName(task.VideoPath)}, 耗时 {sw.ElapsedMilliseconds / 1000.0:F1}s");
            task.State = ThumbnailState.Pending; // 重置以便下次重试
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log($"[Generate] 异常: {ex.GetType().Name}: {ex.Message}, 耗时 {sw.ElapsedMilliseconds / 1000.0:F1}s");
            task.State = ThumbnailState.Failed;
        }
        finally
        {
            _currentProcess?.Dispose();
            _currentProcess = null;
            _currentTask = null;

            // 清理 tmp 目录（失败或取消时）
            try { if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true); } catch { }

            SaveIndex();
            UpdateProgress();
        }
    }

    // ========== 安全关闭 ==========

    public void Shutdown()
    {
        var sw = Stopwatch.StartNew();
        Log("[Shutdown] 开始安全关闭");

        _isShuttingDown = true;

        // 取消循环 + 过期清理
        _loopCts?.Cancel();
        _expiryCts?.Cancel();

        // 等待循环任务完整退出（GenerateForTask 的 finally 会清理 _currentProcess）
        if (_loopTask != null)
        {
            try { _loopTask.Wait(5000); } catch { }
        }

        // 兜底：如果循环退出后进程还在，强制清理
        if (_currentProcess != null && !_currentProcess.HasExited)
        {
            Log($"[Shutdown] 兜底终止 ffmpeg PID={_currentProcess.Id}");
            try
            {
                _currentProcess.StandardInput.Close();
                if (!_currentProcess.WaitForExit(2000))
                    _currentProcess.Kill(entireProcessTree: true);
            }
            catch { }
        }
        _currentProcess?.Dispose();
        _currentProcess = null;
        _currentTask = null;

        // 清理残留 tmp 目录（延后确保文件句柄已释放）
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
        int expiryDays = 30;
        try
        {
            var settings = SettingsService.Instance;
            expiryDays = settings.GetThumbnailExpiryDays();
        }
        catch { }

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
            if (File.Exists(_indexPath))
            {
                string json = File.ReadAllText(_indexPath);
                var entries = JsonSerializer.Deserialize<Dictionary<string, ThumbnailEntryDto>>(json);
                if (entries != null)
                {
                    foreach (var kv in entries)
                    {
                        // 去重：如果已通过 EnqueueFolder 添加，跳过
                        lock (_taskLock)
                        {
                            if (_videoToTask.ContainsKey(kv.Key)) continue;
                        }

                        var state = kv.Value.State switch
                        {
                            "Ready" => ThumbnailState.Ready,
                            "Generating" => ThumbnailState.Pending,
                            "Failed" => ThumbnailState.Pending, // 重启重试
                            _ => ThumbnailState.Pending
                        };

                        string md5Dir = kv.Value.Md5;
                        string fullDir = Path.Combine(_thumbBaseDir, md5Dir);

                        // 磁盘上已有数据 → 直接标记 Ready
                        if (Directory.Exists(fullDir) && state != ThumbnailState.Ready)
                        {
                            var files = Directory.GetFiles(fullDir, "*.jpg");
                            if (files.Length > 0)
                            {
                                state = ThumbnailState.Ready;
                                Log($"[LoadIndex] 磁盘目录已存在 {files.Length} 帧，标记 Ready: {Path.GetFileName(kv.Key)}");
                            }
                        }
                        else if (state == ThumbnailState.Ready && !Directory.Exists(fullDir))
                        {
                            state = ThumbnailState.Pending;
                            Log($"[LoadIndex] 目录缺失，重置为 Pending: {kv.Key}");
                        }

                        int totalFrames = state == ThumbnailState.Ready
                            ? (Directory.Exists(fullDir) ? Directory.GetFiles(fullDir, "*.jpg").Length : kv.Value.TotalFrames)
                            : 0;

                        var task = new ThumbnailTask
                        {
                            VideoPath = kv.Key,
                            Md5Dir = md5Dir,
                            State = state,
                            TotalFrames = totalFrames,
                            Priority = int.MaxValue,
                            MarkedForDeletionAt = kv.Value.MarkedForDeletionAt
                        };

                        lock (_taskLock)
                        {
                            _tasks.Add(task);
                            _videoToTask[kv.Key] = task;
                            _totalCount++;
                            if (task.State == ThumbnailState.Ready) _readyCount++;
                        }
                    }
                }
                sw.Stop();
                Log($"[LoadIndex] 加载完成, {_totalCount} 个条目 ({_readyCount} 就绪), 耗时 {sw.ElapsedMilliseconds}ms");
            }
            else
            {
                Log("[LoadIndex] index.json 不存在，跳过");
            }
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
            var entries = new Dictionary<string, ThumbnailEntryDto>();
            lock (_taskLock)
            {
                foreach (var t in _tasks)
                {
                    entries[t.VideoPath] = new ThumbnailEntryDto
                    {
                        Md5 = t.Md5Dir,
                        State = t.State.ToString(),
                        TotalFrames = t.TotalFrames,
                        MarkedForDeletionAt = t.MarkedForDeletionAt
                    };
                }
            }

            string json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_indexPath, json);
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

        // 在主线程触发事件
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            ProgressChanged?.Invoke(this, new ThumbnailProgressEventArgs { Ready = ready, Total = total });
        });
    }

    // ========== 工具 ==========

    private double GetVideoDuration(string videoPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath.Replace("ffmpeg.exe", "ffprobe.exe"),
                Arguments = $"-v quiet -show_entries format=duration -of csv=p=0 \"{videoPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return 0;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            if (double.TryParse(output.Trim(), out var sec))
                return sec;
        }
        catch (Exception ex)
        {
            Log($"[GetDuration] ffprobe 失败: {ex.Message}");
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
