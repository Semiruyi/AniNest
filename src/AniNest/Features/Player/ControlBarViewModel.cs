using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniNest.Infrastructure.Localization;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Presentation;
using AniNest.Features.Player.Models;
using AniNest.Features.Player.Services;

namespace AniNest.Features.Player;

public partial class ControlBarViewModel : ObservableObject
{
    private readonly IPlayerPlaybackFacade _playbackFacade;
    private readonly ILocalizationService _loc;
    private readonly ISettingsService _settings;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly PlayerPlaybackStateController _playback;
    private readonly PropertyChangedEventHandler _locPropertyChangedHandler;
    private readonly PropertyChangedEventHandler _playbackPropertyChangedHandler;

    private float _savedRate = 1.0f;
    private int _lastNonZeroVolume = 70;

    public ThumbnailPreviewController ThumbnailPreview { get; }

    public bool IsPlaying => _playback.IsPlaying;
    public long CurrentTime
    {
        get => _playback.CurrentTime;
        set
        {
            if (_playback.CurrentTime == value) return;
            _playbackFacade.SeekTo(value);
        }
    }
    public long TotalTime => _playback.TotalTime;
    public string CurrentTimeText => _playback.CurrentTimeText;
    public string TotalTimeText => _playback.TotalTimeText;
    public float Rate { get; private set; } = 1.0f;
    public int Volume { get; private set; }
    public bool IsMuted { get; private set; }
    public long BufferedPosition => _playback.BufferedPosition;
    public bool IsSeeking
    {
        get => _playback.IsSeeking;
        set => _playback.SetSeeking(value);
    }

    public static float[] SpeedOptions { get; } = { 2.0f, 1.5f, 1.25f, 1.0f, 0.75f, 0.5f };

    [ObservableProperty]
    private bool _isSpeedPopupOpen;

    public string? CurrentVideoPath => _playback.CurrentVideoPath;

    public long MediaLength => _playback.MediaLength;

    public string PlayPauseTooltip => _loc["Player.PlayPause"];
    public string PreviousTooltip => _loc["Player.Previous"];
    public string NextTooltip => _loc["Player.Next"];
    public string VolumeTooltip => IsMuted ? _loc["Player.Unmute"] : _loc["Player.Mute"];

    public event Action? NextRequested;
    public event Action? PreviousRequested;
    public event Action? GoBackRequested;
    public event Action? TogglePlaylistRequested;
    public event Action? ToggleFullscreenRequested;

    public ControlBarViewModel(
        IPlayerPlaybackFacade playbackFacade,
        ILocalizationService loc,
        ISettingsService settings,
        IUiDispatcher uiDispatcher,
        PlayerPlaybackStateController playback)
    {
        _playbackFacade = playbackFacade;
        _loc = loc;
        _settings = settings;
        _uiDispatcher = uiDispatcher;
        _playback = playback;
        _locPropertyChangedHandler = (_, args) =>
        {
            if (args.PropertyName is null || args.PropertyName == nameof(ILocalizationService.CurrentLanguage) || args.PropertyName == "Item[]")
            {
                OnPropertyChanged(nameof(PlayPauseTooltip));
                OnPropertyChanged(nameof(PreviousTooltip));
                OnPropertyChanged(nameof(NextTooltip));
                OnPropertyChanged(nameof(VolumeTooltip));
            }
        };
        _playbackPropertyChangedHandler = (_, args) =>
        {
            if (args.PropertyName is null)
                return;

            switch (args.PropertyName)
            {
                case nameof(PlayerPlaybackStateController.IsPlaying):
                    OnPropertyChanged(nameof(IsPlaying));
                    break;
                case nameof(PlayerPlaybackStateController.CurrentTime):
                    OnPropertyChanged(nameof(CurrentTime));
                    break;
                case nameof(PlayerPlaybackStateController.TotalTime):
                    OnPropertyChanged(nameof(TotalTime));
                    break;
                case nameof(PlayerPlaybackStateController.CurrentTimeText):
                    OnPropertyChanged(nameof(CurrentTimeText));
                    break;
                case nameof(PlayerPlaybackStateController.TotalTimeText):
                    OnPropertyChanged(nameof(TotalTimeText));
                    break;
                case nameof(PlayerPlaybackStateController.BufferedPosition):
                    OnPropertyChanged(nameof(BufferedPosition));
                    break;
                case nameof(PlayerPlaybackStateController.IsSeeking):
                    OnPropertyChanged(nameof(IsSeeking));
                    break;
                case nameof(PlayerPlaybackStateController.CurrentVideoPath):
                    OnPropertyChanged(nameof(CurrentVideoPath));
                    break;
            }
        };

        ThumbnailPreview = new ThumbnailPreviewController(
            _playbackFacade,
            () => CurrentVideoPath,
            () => _playbackFacade.MediaLength);

        _loc.PropertyChanged += _locPropertyChangedHandler;
        _playback.PropertyChanged += _playbackPropertyChangedHandler;
        _playback.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PlayerPlaybackStateController.CurrentVideoPath))
                ThumbnailPreview.OnCurrentVideoPathChanged();
        };
        InitializeVolumeState();
        SetRate(_playbackFacade.Rate);
    }

    public void SetRate(float value)
    {
        if (Rate == value) return;
        Rate = value;
        OnPropertyChanged(nameof(Rate));
    }

    [RelayCommand]
    private void PlayPause() => _playbackFacade.TogglePlayPause();


    [RelayCommand]
    private void Stop() => _playbackFacade.Stop();

    [RelayCommand]
    private void Next() => NextRequested?.Invoke();

    [RelayCommand]
    private void Previous() => PreviousRequested?.Invoke();

    [RelayCommand]
    private void Seek(long time)
        => _playbackFacade.SeekTo(time);

    [RelayCommand]
    private void ChangeSpeed(float speed)
    {
        _playbackFacade.Rate = speed;
        SetRate(speed);
    }

    [RelayCommand]
    private void ChangeVolume(double volume)
    {
        ApplyVolume((int)Math.Round(volume));
    }

    [RelayCommand]
    private void ToggleMute()
    {
        if (IsMuted || Volume == 0)
        {
            int restoredVolume = _lastNonZeroVolume > 0 ? _lastNonZeroVolume : 70;
            ApplyVolume(restoredVolume, forceUnmute: true);
            return;
        }

        if (Volume > 0)
            _lastNonZeroVolume = Volume;

        SetMutedState(true);
    }

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private bool _isControlBarVisible = true;

    partial void OnIsFullscreenChanged(bool value)
    {
        IsControlBarVisible = !value;
        if (!value)
        {
            CancelHideTimer();
            _isMouseInShowZone = false;
        }
    }

    private const double ShowZoneHeight = 100;
    private const int HideDelayMs = 350;

    private CancellationTokenSource? _hideTimerCancellation;
    private bool _isMouseInShowZone;

    [RelayCommand]
    private void HandleMouseMove((double mouseY, double containerHeight) args)
    {
        if (!IsFullscreen) return;

        if (args.containerHeight - args.mouseY <= ShowZoneHeight)
        {
            _isMouseInShowZone = true;
            CancelHideTimer();
            IsControlBarVisible = true;
        }
        else if (_isMouseInShowZone)
        {
            _isMouseInShowZone = false;
            StartHideTimer();
        }
    }

    [RelayCommand]
    private void HandleMouseLeave()
    {
        if (!IsFullscreen) return;
        _isMouseInShowZone = false;
        StartHideTimer();
    }

    private void StartHideTimer()
    {
        CancelHideTimer();
        _hideTimerCancellation = new CancellationTokenSource();
        _ = HideAfterDelayAsync(_hideTimerCancellation.Token);
    }

    [RelayCommand]
    private void TogglePlaylist() => TogglePlaylistRequested?.Invoke();

    [RelayCommand]
    private void ToggleFullscreen() => ToggleFullscreenRequested?.Invoke();

    [RelayCommand]
    private void GoBack()
    {
        GoBackRequested?.Invoke();
    }

    [RelayCommand]
    private void EnterRightHold()
    {
        _savedRate = Rate;
        SetRate(3.0f);
        _playbackFacade.Rate = 3.0f;
    }

    [RelayCommand]
    private void ExitRightHold()
    {
        SetRate(_savedRate);
        _playbackFacade.Rate = _savedRate;
    }

    public void Cleanup()
    {
        CancelHideTimer();
        _loc.PropertyChanged -= _locPropertyChangedHandler;
        _playback.PropertyChanged -= _playbackPropertyChangedHandler;
    }

    private async Task HideAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(HideDelayMs, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
            return;

        _uiDispatcher.BeginInvoke(() =>
        {
            if (!cancellationToken.IsCancellationRequested && !_isMouseInShowZone)
                IsControlBarVisible = false;
        });
    }

    private void CancelHideTimer()
    {
        _hideTimerCancellation?.Cancel();
        _hideTimerCancellation?.Dispose();
        _hideTimerCancellation = null;
    }

    private void InitializeVolumeState()
    {
        int configuredVolume = _settings.GetPlayerVolume();
        bool configuredMuted = _settings.GetPlayerMuted();
        _lastNonZeroVolume = configuredVolume > 0 ? configuredVolume : 70;
        Volume = configuredVolume;
        IsMuted = configuredMuted;
        _playbackFacade.Volume = configuredVolume;
        _playbackFacade.IsMuted = configuredMuted;
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(IsMuted));
        OnPropertyChanged(nameof(VolumeTooltip));
    }

    private void ApplyVolume(int volume, bool forceUnmute = false)
    {
        volume = Math.Clamp(volume, 0, 100);
        if (volume > 0)
            _lastNonZeroVolume = volume;

        bool muted = forceUnmute ? false : volume == 0;
        _playbackFacade.Volume = volume;
        _playbackFacade.IsMuted = muted;

        if (Volume != volume)
        {
            Volume = volume;
            OnPropertyChanged(nameof(Volume));
        }

        PersistVolumeState(volume, muted);
    }

    private void SetMutedState(bool muted)
    {
        _playbackFacade.IsMuted = muted;
        PersistVolumeState(Volume, muted);
    }

    private void PersistVolumeState(int volume, bool muted)
    {
        _settings.SetPlayerVolume(volume);
        _settings.SetPlayerMuted(muted);

        bool muteChanged = IsMuted != muted;
        IsMuted = muted;
        if (muteChanged)
            OnPropertyChanged(nameof(IsMuted));

        OnPropertyChanged(nameof(VolumeTooltip));
    }
}
