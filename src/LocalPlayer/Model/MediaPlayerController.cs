using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LibVLCSharp.Shared;

using LibVlcMedia = LibVLCSharp.Shared.Media;

namespace LocalPlayer.Model;

public class MediaPlayerController : IMediaPlayerController
{
    private static readonly Logger Log = AppLog.For<MediaPlayerController>();

    private static LibVLC? _sharedLibVLC;
    private static Task<LibVLC>? _preinitTask;

    private LibVLC? libVLC;
    private MediaPlayer? mediaPlayer;
    private VideoFrameProvider? frameProvider;
    private DispatcherTimer? updateTimer;

    public bool IsPlaying => mediaPlayer?.IsPlaying ?? false;
    public long Time => mediaPlayer?.Time ?? 0;
    public long Length => mediaPlayer?.Length ?? 0;
    public string? CurrentFilePath { get; private set; }
    public WriteableBitmap? VideoBitmap => frameProvider?.Bitmap;

    public float Rate
    {
        get => mediaPlayer?.Rate ?? 1.0f;
        set => mediaPlayer?.SetRate(value);
    }

    public event EventHandler? Playing;
    public event EventHandler? Paused;
    public event EventHandler? Stopped;
    public event EventHandler<ProgressUpdatedEventArgs>? ProgressUpdated;

    static MediaPlayerController()
    {
        _preinitTask = Task.Run(() =>
        {
            Log.Info("[Preinitialize] 开始创建全局 LibVLC...");
            var vlc = new LibVLC();
            _sharedLibVLC = vlc;
            Log.Info("[Preinitialize] 全局 LibVLC 创建成功");
            return vlc;
        });
    }

    public void Initialize()
    {
        try
        {
            Log.Info("开始初始化 LibVLC...");
            var vlc = _sharedLibVLC;
            if (vlc == null)
            {
                vlc = _preinitTask!.GetAwaiter().GetResult();
                Log.Info("等待预热 LibVLC 完成");
            }
            libVLC = vlc;

            mediaPlayer = new MediaPlayer(libVLC);
            Log.Info("MediaPlayer 创建成功");

            frameProvider = new VideoFrameProvider();
            frameProvider.AttachToPlayer(mediaPlayer);
            Log.Info("VideoFrameProvider 已关联 MediaPlayer");
        }
        catch (Exception ex)
        {
            Log.Error("初始化失败", ex);
            throw;
        }

        mediaPlayer.Playing += (s, e) =>
        {
            Log.Info("Playing 事件触发");
            Playing?.Invoke(this, EventArgs.Empty);
        };
        mediaPlayer.Paused += (s, e) => Paused?.Invoke(this, EventArgs.Empty);
        mediaPlayer.Stopped += (s, e) => Stopped?.Invoke(this, EventArgs.Empty);

        updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        updateTimer.Tick += UpdateTimer_Tick;
        updateTimer.Start();

        Log.Info("VLC 初始化完成");
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (mediaPlayer != null && mediaPlayer.Length > 0)
        {
            ProgressUpdated?.Invoke(this, new ProgressUpdatedEventArgs(
                mediaPlayer.Time,
                mediaPlayer.Length
            ));
        }
    }

    public void Play(string filePath, long startTimeMs = 0)
    {
        Log.Info($"Play 被调用: {filePath}, startTimeMs={startTimeMs}");
        if (mediaPlayer == null || libVLC == null)
        {
            Log.Info($"Play 提前返回，mediaPlayer={mediaPlayer}, libVLC={libVLC}");
            return;
        }

        Log.Info($"开始播放: {Path.GetFileName(filePath)}");
        CurrentFilePath = filePath;

        var media = new LibVlcMedia(libVLC, filePath);
        if (startTimeMs > 0)
        {
            media.AddOption($":start-time={startTimeMs / 1000.0:F1}");
        }
        bool result = mediaPlayer.Play(media);
        Log.Info($"mediaPlayer.Play 返回: {result}");
    }

    public void TogglePlayPause()
    {
        if (mediaPlayer == null) return;

        if (mediaPlayer.IsPlaying)
            mediaPlayer.Pause();
        else
            mediaPlayer.Play();
    }

    public void Stop()
    {
        mediaPlayer?.Stop();
    }

    public void SeekForward(long milliseconds)
    {
        if (mediaPlayer == null || mediaPlayer.Length <= 0) return;

        long newTime = Math.Min(mediaPlayer.Length, mediaPlayer.Time + milliseconds);
        mediaPlayer.Time = newTime;
    }

    public void SeekBackward(long milliseconds)
    {
        if (mediaPlayer == null || mediaPlayer.Length <= 0) return;

        long newTime = Math.Max(0, mediaPlayer.Time - milliseconds);
        mediaPlayer.Time = newTime;
    }

    public void SeekTo(long time)
    {
        if (mediaPlayer == null || mediaPlayer.Length <= 0) return;
        long target = Math.Max(0, Math.Min(mediaPlayer.Length, time));
        Log.Debug($"SeekTo {target}ms");
        mediaPlayer.Time = target;
    }

    public static string FormatTime(long milliseconds)
    {
        TimeSpan time = TimeSpan.FromMilliseconds(milliseconds);
        if (time.TotalHours >= 1)
            return time.ToString(@"hh\:mm\:ss");
        else
            return time.ToString(@"mm\:ss");
    }

    public void Dispose()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Log.Info("Dispose 开始");
        updateTimer?.Stop();
        updateTimer = null;
        Log.Info($"updateTimer.Stop 耗时 {sw.ElapsedMilliseconds}ms");

        frameProvider?.Dispose();
        Log.Info($"frameProvider.Dispose 耗时 {sw.ElapsedMilliseconds}ms");
        mediaPlayer?.Stop();
        Log.Info($"mediaPlayer.Stop 耗时 {sw.ElapsedMilliseconds}ms");
        mediaPlayer?.Dispose();
        Log.Info($"mediaPlayer.Dispose 耗时 {sw.ElapsedMilliseconds}ms");
        mediaPlayer = null;

        // libVLC 改为全局单例，不在此处释放，避免下次进入播放页重复初始化
        // libVLC?.Dispose();
        // Log.Info($"libVLC.Dispose 耗时 {sw.ElapsedMilliseconds}ms");
        libVLC = null;
    }
}

public class ProgressUpdatedEventArgs : EventArgs
{
    public long CurrentTime { get; }
    public long TotalTime { get; }

    public ProgressUpdatedEventArgs(long currentTime, long totalTime)
    {
        CurrentTime = currentTime;
        TotalTime = totalTime;
    }
}
