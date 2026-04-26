using System;
using System.IO;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;

namespace LocalPlayer.Services;

public class MediaPlayerController : IDisposable
{
    private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "player.log");

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [MediaPlayerController] {message}{Environment.NewLine}");
        }
        catch { }
    }

    private LibVLC? libVLC;
    private MediaPlayer? mediaPlayer;
    private DispatcherTimer? updateTimer;

    public bool IsPlaying => mediaPlayer?.IsPlaying ?? false;
    public long Time => mediaPlayer?.Time ?? 0;
    public long Length => mediaPlayer?.Length ?? 0;
    public string? CurrentFilePath { get; private set; }

    public event EventHandler? Playing;
    public event EventHandler? Paused;
    public event EventHandler? Stopped;
    public event EventHandler<ProgressUpdatedEventArgs>? ProgressUpdated;

    public void Initialize(VideoView videoView)
    {
        try
        {
            Log("开始初始化 LibVLC...");
            libVLC = new LibVLC();
            Log("LibVLC 创建成功");
            mediaPlayer = new MediaPlayer(libVLC);
            Log("MediaPlayer 创建成功");
            videoView.MediaPlayer = mediaPlayer;
            Log("VideoView 已关联 MediaPlayer");
        }
        catch (Exception ex)
        {
            Log($"初始化失败: {ex.Message}");
            Log($"异常堆栈: {ex.StackTrace}");
            throw;
        }

        mediaPlayer.Playing += (s, e) =>
        {
            Log("Playing 事件触发");
            Playing?.Invoke(this, EventArgs.Empty);
        };
        mediaPlayer.Paused += (s, e) => Paused?.Invoke(this, EventArgs.Empty);
        mediaPlayer.Stopped += (s, e) => Stopped?.Invoke(this, EventArgs.Empty);

        updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        updateTimer.Tick += UpdateTimer_Tick;
        updateTimer.Start();

        Log("VLC 初始化完成");
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

    public void Play(string filePath)
    {
        Log($"Play 被调用: {filePath}");
        if (mediaPlayer == null || libVLC == null)
        {
            Log($"Play 提前返回，mediaPlayer={mediaPlayer}, libVLC={libVLC}");
            return;
        }

        Log($"开始播放: {Path.GetFileName(filePath)}");
        CurrentFilePath = filePath;

        var media = new Media(libVLC, filePath);
        bool result = mediaPlayer.Play(media);
        Log($"mediaPlayer.Play 返回: {result}");
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
        Console.WriteLine($"[MediaPlayerController] SeekTo {target}ms");
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
        updateTimer?.Stop();
        updateTimer = null;
        updateTimer = null;

        mediaPlayer?.Stop();
        mediaPlayer?.Dispose();
        mediaPlayer = null;

        libVLC?.Dispose();
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
