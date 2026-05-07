using System;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalPlayer.Infrastructure.Localization;
using LocalPlayer.Features.Player.Models;
using LocalPlayer.Features.Player.Services;

namespace LocalPlayer.Features.Player;

public partial class ControlBarViewModel : ObservableObject
{
    private readonly IPlayerPlaybackFacade _playbackFacade;
    private readonly ILocalizationService _loc;
    private readonly PlayerPlaybackStateController _playback;
    private readonly PropertyChangedEventHandler _locPropertyChangedHandler;
    private readonly PropertyChangedEventHandler _playbackPropertyChangedHandler;

    private float _savedRate = 1.0f;

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
    public long BufferedPosition => _playback.BufferedPosition;
    public bool IsSeeking
    {
        get => _playback.IsSeeking;
        set => _playback.SetSeeking(value);
    }

    public static float[] SpeedOptions { get; } = { 2.0f, 1.5f, 1.25f, 1.0f, 0.75f, 0.5f };

    [ObservableProperty]
    private bool _isSpeedPopupOpen;

    [RelayCommand]
    private void ToggleSpeedPopup() => IsSpeedPopupOpen = !IsSpeedPopupOpen;

    public string? CurrentVideoPath => _playback.CurrentVideoPath;

    public long MediaLength => _playback.MediaLength;

    public string PlayPauseTooltip => _loc["Player.PlayPause"];
    public string PreviousTooltip => _loc["Player.Previous"];
    public string NextTooltip => _loc["Player.Next"];

    public event Action? NextRequested;
    public event Action? PreviousRequested;
    public event Action? GoBackRequested;
    public event Action? TogglePlaylistRequested;
    public event Action? ToggleFullscreenRequested;

    public ControlBarViewModel(
        IPlayerPlaybackFacade playbackFacade,
        ILocalizationService loc,
        PlayerPlaybackStateController playback)
    {
        _playbackFacade = playbackFacade;
        _loc = loc;
        _playback = playback;
        _locPropertyChangedHandler = (_, args) =>
        {
            if (args.PropertyName is null || args.PropertyName == nameof(ILocalizationService.CurrentLanguage) || args.PropertyName == "Item[]")
            {
                OnPropertyChanged(nameof(PlayPauseTooltip));
                OnPropertyChanged(nameof(PreviousTooltip));
                OnPropertyChanged(nameof(NextTooltip));
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

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private bool _isControlBarVisible = true;

    partial void OnIsFullscreenChanged(bool value)
    {
        IsControlBarVisible = !value;
        if (!value)
        {
            _hideTimer?.Stop();
            _isMouseInShowZone = false;
        }
    }

    private const double ShowZoneHeight = 100;
    private const int HideDelayMs = 350;

    private DispatcherTimer? _hideTimer;
    private bool _isMouseInShowZone;

    [RelayCommand]
    private void HandleMouseMove((double mouseY, double containerHeight) args)
    {
        if (!IsFullscreen) return;

        if (args.containerHeight - args.mouseY <= ShowZoneHeight)
        {
            _isMouseInShowZone = true;
            _hideTimer?.Stop();
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
        if (_hideTimer == null)
        {
            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HideDelayMs) };
            _hideTimer.Tick += (_, _) =>
            {
                _hideTimer.Stop();
                if (!_isMouseInShowZone)
                    IsControlBarVisible = false;
            };
        }
        else
        {
            _hideTimer.Stop();
        }
        _hideTimer.Start();
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
        _loc.PropertyChanged -= _locPropertyChangedHandler;
        _playback.PropertyChanged -= _playbackPropertyChangedHandler;
    }
}
