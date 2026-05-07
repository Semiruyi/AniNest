using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using LocalPlayer.Infrastructure.Diagnostics;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Interop;
using LibVlcMedia = LibVLCSharp.Shared.Media;
namespace LocalPlayer.Infrastructure.Media;

public class MediaPlayerController : IMediaPlayerController
{
    private static readonly Logger Log = AppLog.For<MediaPlayerController>();

    private static LibVLC? _sharedLibVLC;
    private static readonly object _preinitLock = new();
    private static Task<LibVLC>? _preinitTask;

    private LibVLC? libVLC;
    private MediaPlayer? mediaPlayer;
    private VideoFrameProvider? frameProvider;
    private LibVlcMedia? currentMedia;
    private DispatcherTimer? updateTimer;
    private PerfSpan? _playToPlayingSpan;
    private PerfSpan? _playToFirstFrameSpan;
    private PerfSpan? _playToFirstLockSpan;
    private PerfSpan? _playToFirstUnlockSpan;
    private PerfSpan? _playToFirstDisplayQueuedSpan;

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

    private static Task<LibVLC> GetOrStartPreinitializationTask()
    {
        if (_sharedLibVLC != null)
            return Task.FromResult(_sharedLibVLC);

        lock (_preinitLock)
        {
            if (_sharedLibVLC != null)
                return Task.FromResult(_sharedLibVLC);

            if (_preinitTask != null)
                return _preinitTask;

            _preinitTask = Task.Run(() =>
            {
                var vlc = new LibVLC();
                _sharedLibVLC = vlc;
                return vlc;
            });
            return _preinitTask;
        }
    }

    public void Initialize()
    {
        try
        {
            var vlc = _sharedLibVLC;
            if (vlc == null)
            {
                vlc = GetOrStartPreinitializationTask().GetAwaiter().GetResult();
            }
            libVLC = vlc;

            mediaPlayer = new MediaPlayer(libVLC);

            frameProvider = new VideoFrameProvider();
            frameProvider.AttachToPlayer(mediaPlayer);
            frameProvider.FramePresented += OnFramePresented;
            frameProvider.FirstFrameLocked += OnFirstFrameLocked;
            frameProvider.FirstFrameUnlocked += OnFirstFrameUnlocked;
            frameProvider.FirstFrameDisplayQueued += OnFirstFrameDisplayQueued;
            Log.Info("VideoFrameProvider attached to MediaPlayer");
            Log.Info($"Initialize complete: VideoBitmap={(frameProvider.Bitmap == null ? "null" : "ready")}");
        }
        catch (Exception ex)
        {
            Log.Error("Initialization failed", ex);
            throw;
        }

        mediaPlayer.Playing += (s, e) =>
        {
            Log.Info("Playing event raised");
            _playToPlayingSpan?.Dispose();
            _playToPlayingSpan = null;
            Playing?.Invoke(this, EventArgs.Empty);
        };
        mediaPlayer.Paused += (s, e) => Paused?.Invoke(this, EventArgs.Empty);
        mediaPlayer.Stopped += (s, e) => Stopped?.Invoke(this, EventArgs.Empty);

        updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        updateTimer.Tick += UpdateTimer_Tick;
        updateTimer.Start();

        Log.Info("VLC initialized");
    }

    public Task WarmupAsync()
        => GetOrStartPreinitializationTask();

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
        Log.Info($"Play called: {filePath}, startTimeMs={startTimeMs}");
        if (mediaPlayer == null || libVLC == null)
        {
            Log.Info($"Play returned early: mediaPlayer={mediaPlayer}, libVLC={libVLC}");
            return;
        }

        Log.Info($"Start playback: {Path.GetFileName(filePath)}");
        CurrentFilePath = filePath;
        frameProvider?.BeginFrameObservation(filePath);
        _playToPlayingSpan?.Dispose();
        _playToFirstFrameSpan?.Dispose();
        _playToFirstLockSpan?.Dispose();
        _playToFirstUnlockSpan?.Dispose();
        _playToFirstDisplayQueuedSpan?.Dispose();
        var tags = new Dictionary<string, string>
        {
            ["file"] = Path.GetFileName(filePath),
            ["startTimeMs"] = startTimeMs.ToString()
        };
        _playToPlayingSpan = PerfSpan.Begin("Media.PlayToPlaying", tags);
        _playToFirstFrameSpan = PerfSpan.Begin("Media.PlayToFirstFrame", tags);
        _playToFirstLockSpan = PerfSpan.Begin("Media.PlayToFirstFrameLock", tags);
        _playToFirstUnlockSpan = PerfSpan.Begin("Media.PlayToFirstFrameUnlock", tags);
        _playToFirstDisplayQueuedSpan = PerfSpan.Begin("Media.PlayToFirstFrameDisplayQueued", tags);

        ReleaseCurrentMedia();

        currentMedia = new LibVlcMedia(libVLC, filePath);
        if (startTimeMs > 0)
        {
            currentMedia.AddOption($":start-time={startTimeMs / 1000.0:F1}");
        }
        bool result = mediaPlayer.Play(currentMedia);
        Log.Info($"mediaPlayer.Play returned: {result}");
        Log.Info($"After Play: IsPlaying={mediaPlayer.IsPlaying}, Time={mediaPlayer.Time}, Length={mediaPlayer.Length}");
    }

    public void TogglePlayPause()
    {
        if (mediaPlayer == null) return;

        if (mediaPlayer.IsPlaying)
        {
            Log.Info("TogglePlayPause -> Pause");
            mediaPlayer.Pause();
        }
        else
        {
            Log.Info("TogglePlayPause -> Play");
            mediaPlayer.Play();
        }
    }

    public void Stop()
    {
        Log.Info("Stop called");
        mediaPlayer?.Stop();
    }

    public void ResetSession()
    {
        Log.Info("ResetSession called");
        _playToPlayingSpan?.Dispose();
        _playToPlayingSpan = null;
        _playToFirstFrameSpan?.Dispose();
        _playToFirstFrameSpan = null;
        _playToFirstLockSpan?.Dispose();
        _playToFirstLockSpan = null;
        _playToFirstUnlockSpan?.Dispose();
        _playToFirstUnlockSpan = null;
        _playToFirstDisplayQueuedSpan?.Dispose();
        _playToFirstDisplayQueuedSpan = null;

        mediaPlayer?.Stop();
        ReleaseCurrentMedia();
        CurrentFilePath = null;
        frameProvider?.BeginFrameObservation(null);
        frameProvider?.ClearBitmap();
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
        Log.Debug($"SeekTo request={time} target={target} current={mediaPlayer.Time} length={mediaPlayer.Length} isPlaying={mediaPlayer.IsPlaying}");
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
        Log.Info("Dispose started");
        updateTimer?.Stop();
        updateTimer = null;

        frameProvider?.Dispose();
        if (frameProvider != null)
        {
            frameProvider.FramePresented -= OnFramePresented;
            frameProvider.FirstFrameLocked -= OnFirstFrameLocked;
            frameProvider.FirstFrameUnlocked -= OnFirstFrameUnlocked;
            frameProvider.FirstFrameDisplayQueued -= OnFirstFrameDisplayQueued;
        }
        mediaPlayer?.Stop();
        ReleaseCurrentMedia();
        mediaPlayer?.Dispose();
        mediaPlayer = null;

        libVLC = null;
    }

    private void OnFramePresented(object? sender, EventArgs e)
    {
        if (_playToFirstFrameSpan == null)
            return;

        _playToFirstFrameSpan.Dispose();
        _playToFirstFrameSpan = null;
    }

    private void OnFirstFrameLocked(object? sender, EventArgs e)
    {
        _playToFirstLockSpan?.Dispose();
        _playToFirstLockSpan = null;
    }

    private void OnFirstFrameUnlocked(object? sender, EventArgs e)
    {
        _playToFirstUnlockSpan?.Dispose();
        _playToFirstUnlockSpan = null;
    }

    private void OnFirstFrameDisplayQueued(object? sender, EventArgs e)
    {
        _playToFirstDisplayQueuedSpan?.Dispose();
        _playToFirstDisplayQueuedSpan = null;
    }

    private void ReleaseCurrentMedia()
    {
        currentMedia?.Dispose();
        currentMedia = null;
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



