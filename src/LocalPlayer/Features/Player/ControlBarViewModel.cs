using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LocalPlayer.Core.Messaging;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Interop;
using LocalPlayer.Core.Localization;
using LocalPlayer.Presentation.Converters;
using LocalPlayer.Features.Player.Models;

namespace LocalPlayer.Features.Player;

public partial class ControlBarViewModel : ObservableObject
{
    private readonly IMediaPlayerController _media;
    private readonly PlayerInputHandler _inputHandler;
    private readonly ILocalizationService _loc;

    private float _savedRate = 1.0f;
    private long _lastNonZeroTime;

    // ========== 缂╃暐鍥鹃瑙堬紙缁勫悎锛?==========

    public ThumbnailPreviewController ThumbnailPreview { get; }

    // ========== 鎾斁鐘舵€?==========

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private long _currentTime;

    [ObservableProperty]
    private long _totalTime;

    [ObservableProperty]
    private string _currentTimeText = "00:00";

    [ObservableProperty]
    private string _totalTimeText = "00:00";

    [ObservableProperty]
    private float _rate = 1.0f;

    [ObservableProperty]
    private long _bufferedPosition;

    [ObservableProperty]
    private bool _isSeeking;

    // ========== 鍊嶉€熷脊绐?==========

    public static float[] SpeedOptions { get; } = { 2.0f, 1.5f, 1.25f, 1.0f, 0.75f, 0.5f };

    [ObservableProperty]
    private bool _isSpeedPopupOpen;

    [RelayCommand]
    private void ToggleSpeedPopup() => IsSpeedPopupOpen = !IsSpeedPopupOpen;

    [ObservableProperty]
    private string? _currentVideoPath;

    public long MediaLength => _media.Length;

    // ========== 鎻愮ず ==========

    public string PlayPauseTooltip => FormatTooltip(_loc["Player.PlayPause"], "TogglePlayPause");
    public string PreviousTooltip => FormatTooltip(_loc["Player.Previous"], "PreviousEpisode");
    public string NextTooltip => FormatTooltip(_loc["Player.Next"], "NextEpisode");

    private string FormatTooltip(string label, string actionName)
    {
        var bindings = _inputHandler.GetCurrentBindings();
        var key = bindings.TryGetValue(actionName, out var k) ? k : Key.None;
        return key == Key.None ? label : $"{label} ({KeyDisplayConverter.Format(key)})";
    }

    // ========== 璺ㄧ粍浠朵簨浠?==========

    public event Action? NextRequested;
    public event Action? PreviousRequested;
    public event Action? GoBackRequested;
    public event Action? TogglePlaylistRequested;

    // ========== 鏋勯€?==========

    public ControlBarViewModel(IMediaPlayerController media, PlayerInputHandler inputHandler,
                               IThumbnailGenerator thumbnailGenerator, ILocalizationService loc)
    {
        _media = media;
        _inputHandler = inputHandler;
        _loc = loc;

        ThumbnailPreview = new ThumbnailPreviewController(
            thumbnailGenerator,
            () => CurrentVideoPath,
            () => _media.Length);

        _inputHandler.BindingsChanged += () =>
        {
            OnPropertyChanged(nameof(PlayPauseTooltip));
            OnPropertyChanged(nameof(PreviousTooltip));
            OnPropertyChanged(nameof(NextTooltip));
        };

        _media.Playing += (_, _) =>
            Application.Current.Dispatcher.Invoke(() => IsPlaying = true);

        _media.Paused += (_, _) =>
            Application.Current.Dispatcher.Invoke(() => IsPlaying = false);

        _media.Stopped += (_, _) =>
            Application.Current.Dispatcher.Invoke(() => IsPlaying = false);

        _media.ProgressUpdated += (_, args) =>
        {
            if (IsSeeking) return;

            // 鏂拌棰戝姞杞戒腑鎴?VLC 閲?seek 鏈熼棿锛欳urrentTime=0 涓轰腑闂存€侊紝鎶戝埗閬垮厤闂儊
            if (args.CurrentTime == 0 && IsPlaying)
            {
                if (_lastNonZeroTime > 0)
                    CurrentTime = _lastNonZeroTime;
                TotalTime = args.TotalTime;
                return;
            }

            _lastNonZeroTime = args.CurrentTime;
            CurrentTime = args.CurrentTime;
            TotalTime = args.TotalTime;
            BufferedPosition = args.TotalTime; // local files are fully buffered
            CurrentTimeText = MediaPlayerController.FormatTime(args.CurrentTime);
            TotalTimeText = MediaPlayerController.FormatTime(args.TotalTime);
        };
    }

    // ========== 鎾斁鍛戒护 ==========

    [RelayCommand]
    private void PlayPause() => _media.TogglePlayPause();

    [RelayCommand]
    private void Stop() => _media.Stop();

    [RelayCommand]
    private void Next() => NextRequested?.Invoke();

    [RelayCommand]
    private void Previous() => PreviousRequested?.Invoke();

    [RelayCommand]
    private void Seek(long time) => _media.SeekTo(time);

    [RelayCommand]
    private void ChangeSpeed(float speed)
    {
        _media.Rate = speed;
        Rate = speed;
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

    // ========== 鍏ㄥ睆鏃堕紶鏍囨帶鍒舵爮鏄鹃殣 ==========

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
    private void ToggleFullscreen() => WeakReferenceMessenger.Default.Send(new ToggleFullscreenMessage());

    [RelayCommand]
    private void GoBack()
    {
        GoBackRequested?.Invoke();
    }

    // ========== 鍙抽敭鎸変綇 ==========

    [RelayCommand]
    private void EnterRightHold()
    {
        _savedRate = Rate;
        Rate = 3.0f;
        _media.Rate = 3.0f;
    }

    [RelayCommand]
    private void ExitRightHold()
    {
        Rate = _savedRate;
        _media.Rate = _savedRate;
    }

    // ========== 閿洏 ==========

    public bool HandleKeyDown(KeyEventArgs e) => _inputHandler.HandleKeyDown(e);

    [RelayCommand]
    private void KeyDown(KeyEventArgs e) => HandleKeyDown(e);

}





