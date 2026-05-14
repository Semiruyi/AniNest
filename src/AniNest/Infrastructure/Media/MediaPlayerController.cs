using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using AniNest.Infrastructure.Diagnostics;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Interop;
using LibVlcMedia = LibVLCSharp.Shared.Media;
namespace AniNest.Infrastructure.Media;

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
    private readonly EventHandler<EventArgs> _playingHandler;
    private readonly EventHandler<EventArgs> _pausedHandler;
    private readonly EventHandler<EventArgs> _stoppedHandler;
    private PerfSpan? _playToPlayingSpan;
    private PerfSpan? _playToFirstFrameSpan;
    private PerfSpan? _playToFirstLockSpan;
    private PerfSpan? _playToFirstUnlockSpan;
    private PerfSpan? _playToFirstDisplayQueuedSpan;
    private int _volume = 100;
    private bool _isMuted;

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

    public int Volume
    {
        get => mediaPlayer?.Volume ?? _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 100);
            if (mediaPlayer != null)
                mediaPlayer.Volume = _volume;
        }
    }

    public bool IsMuted
    {
        get => mediaPlayer?.Mute ?? _isMuted;
        set
        {
            _isMuted = value;
            if (mediaPlayer != null)
                mediaPlayer.Mute = value;
        }
    }

    public event EventHandler? Playing;
    public event EventHandler? Paused;
    public event EventHandler? Stopped;
    public event EventHandler<ProgressUpdatedEventArgs>? ProgressUpdated;

    public MediaPlayerController()
    {
        _playingHandler = (_, _) =>
        {
            _playToPlayingSpan?.Dispose();
            _playToPlayingSpan = null;
            Playing?.Invoke(this, EventArgs.Empty);
        };
        _pausedHandler = (_, _) => Paused?.Invoke(this, EventArgs.Empty);
        _stoppedHandler = (_, _) => Stopped?.Invoke(this, EventArgs.Empty);
    }

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

    public async Task InitializeAsync()
    {
        try
        {
            var vlc = _sharedLibVLC;
            if (vlc == null)
            {
                vlc = await GetOrStartPreinitializationTask().ConfigureAwait(false);
            }
            libVLC = vlc;
            EnsurePlaybackSession();
            Log.Info(MemorySnapshot.Capture("MediaPlayerController.Initialize",
                ("mediaPlayer", mediaPlayer != null),
                ("frameProvider", frameProvider != null),
                ("bitmap", frameProvider?.Bitmap != null),
                ("sharedLibVlc", _sharedLibVLC != null)));
        }
        catch (Exception ex)
        {
            Log.Error("Initialization failed", ex);
            throw;
        }
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

    public bool TryPlay(string filePath, long startTimeMs, out string? errorMessage)
    {
        errorMessage = null;

        EnsurePlaybackSession();
        if (mediaPlayer == null || libVLC == null)
        {
            errorMessage = "Media player is not initialized.";
            return false;
        }

        if (!File.Exists(filePath))
        {
            errorMessage = $"Media file not found: {filePath}";
            return false;
        }

        try
        {
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

            mediaPlayer.Play(currentMedia);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Play failed: file={filePath}", ex);
            errorMessage = ex.Message;
            ReleaseCurrentMedia();
            return false;
        }
    }

    public void TogglePlayPause()
    {
        if (mediaPlayer == null) return;

        if (mediaPlayer.IsPlaying)
        {
            mediaPlayer.Pause();
        }
        else
        {
            mediaPlayer.Play();
        }
    }

    public void Stop()
    {
        mediaPlayer?.Stop();
    }

    public void ResetSession()
    {
        Log.Info("ResetSession called");
        Log.Info(MemorySnapshot.Capture("MediaPlayerController.ResetSession.begin",
            ("hasCurrentMedia", currentMedia != null),
            ("currentFile", Path.GetFileName(CurrentFilePath)),
            ("bitmap", frameProvider?.Bitmap != null),
            ("isPlaying", mediaPlayer?.IsPlaying ?? false)));
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

        CurrentFilePath = null;
        RecreatePlaybackSession();
        Log.Info(MemorySnapshot.Capture("MediaPlayerController.ResetSession.end",
            ("hasCurrentMedia", currentMedia != null),
            ("currentFile", Path.GetFileName(CurrentFilePath)),
            ("bitmap", frameProvider?.Bitmap != null),
            ("isPlaying", mediaPlayer?.IsPlaying ?? false)));
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
        Log.Info(MemorySnapshot.Capture("MediaPlayerController.Dispose.begin",
            ("hasCurrentMedia", currentMedia != null),
            ("bitmap", frameProvider?.Bitmap != null),
            ("mediaPlayer", mediaPlayer != null)));
        updateTimer?.Stop();
        updateTimer = null;

        DestroyPlaybackSession(clearBitmap: true, disposeFrameProvider: true);

        libVLC = null;
        Log.Info(MemorySnapshot.Capture("MediaPlayerController.Dispose.end",
            ("hasCurrentMedia", currentMedia != null),
            ("bitmap", frameProvider?.Bitmap != null),
            ("mediaPlayer", mediaPlayer != null)));
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
        if (currentMedia != null)
        {
            Log.Info(MemorySnapshot.Capture("MediaPlayerController.ReleaseCurrentMedia",
                ("currentFile", Path.GetFileName(CurrentFilePath))));
        }
        currentMedia?.Dispose();
        currentMedia = null;
    }

    private void EnsurePlaybackSession()
    {
        if (libVLC == null)
            return;

        if (mediaPlayer != null && frameProvider != null)
            return;

        mediaPlayer = new MediaPlayer(libVLC);
        mediaPlayer.Playing += _playingHandler;
        mediaPlayer.Paused += _pausedHandler;
        mediaPlayer.Stopped += _stoppedHandler;
        mediaPlayer.Volume = _volume;
        mediaPlayer.Mute = _isMuted;

        frameProvider ??= CreateFrameProvider();
        frameProvider.AttachToPlayer(mediaPlayer);

        EnsureUpdateTimer();

    }

    private void RecreatePlaybackSession()
    {
        DestroyPlaybackSession(clearBitmap: true, disposeFrameProvider: false);
        EnsurePlaybackSession();
    }

    private void DestroyPlaybackSession(bool clearBitmap, bool disposeFrameProvider)
    {
        if (clearBitmap)
            frameProvider?.ClearBitmap();

        if (disposeFrameProvider && frameProvider != null)
        {
            frameProvider.FramePresented -= OnFramePresented;
            frameProvider.FirstFrameLocked -= OnFirstFrameLocked;
            frameProvider.FirstFrameUnlocked -= OnFirstFrameUnlocked;
            frameProvider.FirstFrameDisplayQueued -= OnFirstFrameDisplayQueued;
        }

        frameProvider?.BeginFrameObservation(null);
        if (disposeFrameProvider)
        {
            frameProvider?.Dispose();
            frameProvider = null;
        }

        if (mediaPlayer != null)
        {
            mediaPlayer.Playing -= _playingHandler;
            mediaPlayer.Paused -= _pausedHandler;
            mediaPlayer.Stopped -= _stoppedHandler;
            mediaPlayer.Stop();
        }

        ReleaseCurrentMedia();
        mediaPlayer?.Dispose();
        mediaPlayer = null;
    }

    private void EnsureUpdateTimer()
    {
        if (updateTimer != null)
            return;

        updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        updateTimer.Tick += UpdateTimer_Tick;
        updateTimer.Start();
    }

    private VideoFrameProvider CreateFrameProvider()
    {
        var provider = new VideoFrameProvider();
        provider.FramePresented += OnFramePresented;
        provider.FirstFrameLocked += OnFirstFrameLocked;
        provider.FirstFrameUnlocked += OnFirstFrameUnlocked;
        provider.FirstFrameDisplayQueued += OnFirstFrameDisplayQueued;
        return provider;
    }
}
