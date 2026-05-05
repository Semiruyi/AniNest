using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LocalPlayer.Presentation.Diagnostics;

namespace LocalPlayer.Infrastructure.Model;

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
    public long MarkedForDeletionAt { get; set; } // Unix 鏃堕棿鎴筹紝0 = 鏈爣璁?
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
        Log.Info($"鍒濆鍖? thumbBaseDir={_thumbBaseDir}");

        Task.Run(Initialize);
    }

    private void Initialize()
    {
        var sw = Stopwatch.StartNew();
        Log.Info("[Initialize] 寮€濮嬪垵濮嬪寲");

        // 妫€娴?ffmpeg
        _ffmpegAvailable = File.Exists(_ffmpegPath);
        if (!_ffmpegAvailable)
        {
            Log.Info($"[Initialize] ffmpeg 涓嶅彲鐢紝鏈壘鍒? {_ffmpegPath}锛岀缉鐣ュ浘鍔熻兘灏嗕笉宸ヤ綔");
        }
        else
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
                    Log.Info($"[Initialize] ffmpeg 鍙敤: {verOutput}");
                    versionProc.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Info($"[Initialize] ffmpeg 鐗堟湰妫€娴嬪紓甯? {ex.Message}");
            }
        }

        // 娓呯悊娈嬬暀鐨?tmp 鐩綍
        CleanupTempDirs();

        // 鍔犺浇绱㈠紩
        LoadIndex();

        sw.Stop();
        Log.Info($"[Initialize] 鍒濆鍖栧畬鎴? 灏辩华 {_readyCount}/{_totalCount}, 鎬昏€楁椂 {sw.ElapsedMilliseconds}ms");

        // 淇″彿锛氬垵濮嬪寲瀹屾垚锛屽厑璁搁槦鍒楀紑濮嬪鐞?
        _initTcs.TrySetResult();
        EnsureLoopRunning();
        StartExpiryCleanup();
        Log.Info("[Initialize] 宸茶Е鍙戦槦鍒楀鐞?+ 杩囨湡娓呯悊");
    }

    // ========== 鍏ラ槦 ==========

    /// <summary>
    /// 鎵归噺鍏ラ槦鏂囦欢澶逛腑鐨勬墍鏈夎棰戯紝鎸変紭鍏堢骇鎺掑簭銆?
    /// cardOrder 瓒婂皬瓒婂墠闈紱鍚屼竴鍗＄墖鍐?lastPlayed > unplayed > played銆?
    /// </summary>
    public void EnqueueFolder(string folderPath, int cardOrder,
        string? lastPlayedPath, HashSet<string> playedPaths)
    {
        if (!_ffmpegAvailable)
        {
            Log.Info($"[EnqueueFolder] ffmpeg 涓嶅彲鐢紝璺宠繃: {Path.GetFileName(folderPath)}");
            return;
        }

        var sw = Stopwatch.StartNew();
        var videoFiles = VideoScanner.GetVideoFiles(folderPath);
        Log.Info($"[EnqueueFolder] {Path.GetFileName(folderPath)}: {videoFiles.Length} 涓棰? cardOrder={cardOrder}");

        int added = 0;
        foreach (var videoPath in videoFiles)
        {
            // 鍘婚噸 + 娓呴櫎寰呭垹闄ゆ爣璁帮紙閲嶆柊娣诲姞鐨勫崱鐗囷級
            lock (_taskLock)
            {
                if (_videoToTask.TryGetValue(videoPath, out var existing))
                {
                    if (existing.MarkedForDeletionAt != 0)
                    {
                        existing.MarkedForDeletionAt = 0;
                        SaveIndex();
                        Log.Info($"[EnqueueFolder] 娓呴櫎寰呭垹闄ゆ爣璁? {Path.GetFileName(videoPath)}");
                    }
                    continue;
                }
            }

            // 璁＄畻浼樺厛绾?
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
        EnsureLoopRunning(); // 鎬绘槸纭繚寰幆鍦ㄨ繍琛岋紝鍗充娇娌℃湁鏂颁换鍔?

        sw.Stop();
        Log.Info($"[EnqueueFolder] {Path.GetFileName(folderPath)}: 鏂板 {added} 涓换鍔? 鎬昏€楁椂 {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// 鍒犻櫎鏂囦欢澶规椂鏍囪缂╃暐鍥句负寰呭垹闄わ紝鑰岄潪绔嬪嵆鍒犻櫎銆?
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

        // 鏂囦欢澶瑰凡涓嶅瓨鍦ㄦ椂閫氳繃鏄犲皠鍙嶆煡
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
        Log.Info($"[DeleteForFolder] {folderPath}: 鏍囪寰呭垹闄?{marked} 涓? 鎬昏€楁椂 {sw.ElapsedMilliseconds}ms");

        // 鍒楀嚭鎵€鏈夎鏍囪鐨勮棰戣矾寰?
        lock (_taskLock)
        {
            var markedPaths = _tasks.Where(t =>
                t.MarkedForDeletionAt > 0 &&
                t.VideoPath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase))
                .Select(t => t.VideoPath);
            foreach (var p in markedPaths)
                Log.Info($"[DeleteForFolder] 宸叉爣璁? {p}");
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

    // ========== 鏌ヨ ==========

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

        // 鏂囦欢缂栧彿浠?0001 寮€濮?(ffmpeg %04d)
        string path = Path.Combine(_thumbBaseDir, task.Md5Dir, $"{second + 1:D4}.jpg");
        return File.Exists(path) ? path : null;
    }

    // ========== 闃熷垪澶勭悊 ==========

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
            // 鎶?Ready 鍜?Generating 鎺掑埌鏈熬锛堜笉鍙備笌璋冨害锛?
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
                // 闃熷垪绌猴紝绛夊緟鍚庨噸璇?
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
                Log.Error($"[ProcessLoop] 鐢熸垚寮傚父: {ex.GetType().Name}: {ex.Message}");
                task.State = ThumbnailState.Failed;
                SaveIndex();
                UpdateProgress();
            }
        }
        Log.Info("[ProcessLoop] 闃熷垪澶勭悊寰幆缁撴潫");
    }

    private async Task GenerateForTask(ThumbnailTask task, CancellationToken ct)
    {
        task.State = ThumbnailState.Generating;
        SaveIndex();

        try
        {
            // VideoProgress 鏄簨浠讹紝鐩存帴浼犲弬浼氳姹傚€间负蹇収锛堟鏃跺彲鑳芥棤璁㈤槄鑰咃級
            // 鐢?lambda 姣忔鍔ㄦ€?Invoke锛屽悗璁㈤槄鐨勪篃鑳芥敹鍒?
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
            // Renderer 宸叉潃杩涚▼ + 娓?tmp锛岃繖閲岄噸缃姸鎬佷互渚夸笅娆￠噸璇?
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

    // ========== 瀹夊叏鍏抽棴 ==========

    public void Shutdown()
    {
        var sw = Stopwatch.StartNew();
        Log.Info("[Shutdown] starting");

        _isShuttingDown = true;

        // 鍙栨秷寰幆 + 杩囨湡娓呯悊 鈫?Renderer 鍐呴儴鏉€ ffmpeg + 娓?tmp
        Log.Info("[Shutdown] 鍙栨秷 _loopCts...");
        _loopCts?.Cancel();
        Log.Info("[Shutdown] 鍙栨秷 _expiryCts...");
        _expiryCts?.Cancel();

        if (_loopTask != null)
        {
        Log.Info($"[Shutdown] waiting for _loopTask (Status={_loopTask.Status})...");
            var waitSw = Stopwatch.StartNew();
            try { _loopTask.Wait(5000); } catch { }
            waitSw.Stop();
            Log.Info($"[Shutdown] _loopTask 绛夊緟瀹屾垚, 瀹為檯鑰楁椂 {waitSw.ElapsedMilliseconds}ms, Status={_loopTask.Status}");
        }

        // 鍏滃簳娓呯悊娈嬬暀 tmp 鐩綍
        CleanupTempDirs();

        SaveIndex();
        sw.Stop();
        Log.Info($"[Shutdown] 瀹夊叏鍏抽棴瀹屾垚, 鎬昏€楁椂 {sw.ElapsedMilliseconds}ms");
    }

    private void CleanupTempDirs()
    {
        try
        {
            var tmpDirs = Directory.GetDirectories(_thumbBaseDir, ".tmp_*");
            if (tmpDirs.Length > 0)
            {
                Log.Info($"[CleanupTemp] 娓呯悊 {tmpDirs.Length} 涓畫鐣?tmp 鐩綍");
                foreach (var dir in tmpDirs)
                {
                    try { Directory.Delete(dir, true); }
                    catch (Exception ex) { Log.Info($"[CleanupTemp] 鍒犻櫎澶辫触: {dir}, {ex.Message}"); }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[CleanupTemp] 娓呯悊寮傚父: {ex.Message}");
        }
    }

    // ========== 杩囨湡娓呯悊 ==========

    private CancellationTokenSource? _expiryCts;

    private void StartExpiryCleanup()
    {
        _expiryCts = new CancellationTokenSource();
        _ = ExpiryCleanupLoop(_expiryCts.Token);
    }

    private async Task ExpiryCleanupLoop(CancellationToken ct)
    {
        Log.Info("[ExpiryCleanup] 鍚姩杩囨湡娓呯悊寰幆");
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromHours(1), ct); } catch { break; }
            CleanupExpired();
        }
        Log.Info("[ExpiryCleanup] 杩囨湡娓呯悊寰幆缁撴潫");
    }

    private void CleanupExpired()
    {
        int expiryDays = _settings.GetThumbnailExpiryDays();

        if (expiryDays <= 0) return; // 姘镐笉杩囨湡

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
        Log.Info($"[ExpiryCleanup] 鍙戠幇 {expired.Count} 涓繃鏈熺缉鐣ュ浘 (杩囨湡澶╂暟={expiryDays})");

        foreach (var t in expired)
        {
            Log.Info($"[ExpiryCleanup] 鍒犻櫎: 瑙嗛={t.VideoPath}, 鐩綍={t.Md5Dir}");
            string dir = Path.Combine(_thumbBaseDir, t.Md5Dir);
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
                Log.Info($"[ExpiryCleanup] 宸插垹闄ょ洰褰? {dir}");
            }
            catch (Exception ex)
            {
                Log.Error($"[ExpiryCleanup] 鍒犻櫎鐩綍澶辫触: {dir}, {ex.Message}");
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
        Log.Info($"[ExpiryCleanup] 娓呯悊瀹屾垚, 鍒犻櫎 {expired.Count} 涓? 鑰楁椂 {sw.ElapsedMilliseconds}ms");
    }

    // ========== 绱㈠紩鎸佷箙鍖?==========

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
            Log.Info($"[LoadIndex] 鍔犺浇瀹屾垚, {_totalCount} 涓潯鐩?({_readyCount} 灏辩华), 鑰楁椂 {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Info($"[LoadIndex] 鍔犺浇寮傚父: {ex.Message}, 鑰楁椂 {sw.ElapsedMilliseconds}ms");
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
            Log.Error($"[SaveIndex] 淇濆瓨寮傚父: {ex.Message}");
        }
    }

    // ========== 杩涘害 ==========

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

    // ========== 宸ュ叿 ==========

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
                        Log.Info($"ffmpeg 鏃堕暱: {Path.GetFileName(videoPath)}={ts.TotalSeconds:F1}s");
                        return ts.TotalSeconds;
                    }
                }
            }
            Log.Warning($"ffmpeg 鏃堕暱瑙ｆ瀽澶辫触: {Path.GetFileName(videoPath)}");
        }
        catch (Exception ex)
        {
            Log.Warning($"VLC 鑾峰彇鏃堕暱寮傚父: {Path.GetFileName(videoPath)}, {ex.GetType().Name}: {ex.Message}");
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


