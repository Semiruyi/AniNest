using System;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace LocalPlayer.Services;

public class MediaPlayerController : IDisposable
{
    private LibVLC? libVLC;
    private MediaPlayer? mediaPlayer;
    private System.Windows.Forms.Timer? updateTimer;

    public bool IsPlaying => mediaPlayer?.IsPlaying ?? false;
    public long Time => mediaPlayer?.Time ?? 0;
    public long Length => mediaPlayer?.Length ?? 0;
    public int Volume => mediaPlayer?.Volume ?? 0;
    public bool IsMuted => mediaPlayer?.Mute ?? false;

    public event EventHandler? Playing;
    public event EventHandler? Paused;
    public event EventHandler? Stopped;
    public event EventHandler<ProgressUpdatedEventArgs>? ProgressUpdated;

    public void Initialize(VideoView videoView)
    {
        libVLC = new LibVLC();
        mediaPlayer = new MediaPlayer(libVLC);
        videoView.MediaPlayer = mediaPlayer;

        mediaPlayer.Playing += (s, e) => Playing?.Invoke(this, EventArgs.Empty);
        mediaPlayer.Paused += (s, e) => Paused?.Invoke(this, EventArgs.Empty);
        mediaPlayer.Stopped += (s, e) => Stopped?.Invoke(this, EventArgs.Empty);

        updateTimer = new System.Windows.Forms.Timer { Interval = 200 };
        updateTimer.Tick += UpdateTimer_Tick;
        updateTimer.Start();

        Console.WriteLine("[MediaPlayerController] VLC 初始化完成");
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
        if (mediaPlayer == null || libVLC == null) return;

        Console.WriteLine($"[MediaPlayerController] 开始播放: {Path.GetFileName(filePath)}");
        var media = new Media(libVLC, filePath);
        mediaPlayer.Play(media);
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

    public void SetVolume(int volume)
    {
        if (mediaPlayer == null) return;
        mediaPlayer.Volume = Math.Max(0, Math.Min(100, volume));
    }

    public void IncreaseVolume(int amount)
    {
        if (mediaPlayer == null) return;
        mediaPlayer.Volume = Math.Min(100, mediaPlayer.Volume + amount);
    }

    public void DecreaseVolume(int amount)
    {
        if (mediaPlayer == null) return;
        mediaPlayer.Volume = Math.Max(0, mediaPlayer.Volume - amount);
    }

    public void ToggleMute()
    {
        if (mediaPlayer == null) return;
        mediaPlayer.Mute = !mediaPlayer.Mute;
    }

    public void SetMuted(bool muted)
    {
        if (mediaPlayer == null) return;
        mediaPlayer.Mute = muted;
    }

    public void SeekTo(long time)
    {
        if (mediaPlayer == null || mediaPlayer.Length <= 0) return;
        mediaPlayer.Time = Math.Max(0, Math.Min(mediaPlayer.Length, time));
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
        updateTimer?.Dispose();
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
