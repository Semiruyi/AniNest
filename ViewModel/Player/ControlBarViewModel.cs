using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LocalPlayer.Messages;
using LocalPlayer.Model;

namespace LocalPlayer.ViewModel.Player;

public partial class ControlBarViewModel : ObservableObject
{
    private readonly IMediaPlayerController _media;
    private readonly PlayerInputHandler _inputHandler;

    private float _savedRate = 1.0f;
    private long _lastNonZeroTime;

    // ========== 缩略图预览（组合） ==========

    public ThumbnailPreviewController ThumbnailPreview { get; }

    // ========== 播放状态 ==========

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

    public static float[] SpeedOptions { get; } = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f, 3f };

    [ObservableProperty]
    private bool _isSeeking;

    [ObservableProperty]
    private string? _currentVideoPath;

    public long MediaLength => _media.Length;

    // ========== 倍速弹窗 ==========

    [ObservableProperty]
    private bool _isSpeedPopupOpen;

    private DispatcherTimer? _speedCloseTimer;

    public void OnSpeedEnter()
    {
        _speedCloseTimer?.Stop();
        IsSpeedPopupOpen = true;
    }

    [RelayCommand]
    private void SpeedEnter() => OnSpeedEnter();

    [RelayCommand]
    private void SpeedLeave() => OnSpeedLeave();

    public void OnSpeedLeave()
    {
        if (_speedCloseTimer == null)
        {
            _speedCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _speedCloseTimer.Tick += (_, _) =>
            {
                _speedCloseTimer.Stop();
                IsSpeedPopupOpen = false;
            };
        }
        else
        {
            _speedCloseTimer.Stop();
        }
        _speedCloseTimer.Start();
    }

    public void CloseSpeedPopup()
    {
        _speedCloseTimer?.Stop();
        IsSpeedPopupOpen = false;
    }

    // ========== 提示 ==========

    public string PlayPauseTooltip => FormatTooltip("播放/暂停", "TogglePlayPause");
    public string PreviousTooltip => FormatTooltip("上一集", "PreviousEpisode");
    public string NextTooltip => FormatTooltip("下一集", "NextEpisode");

    private string FormatTooltip(string label, string actionName)
    {
        var bindings = _inputHandler.GetCurrentBindings();
        var key = bindings.TryGetValue(actionName, out var k) ? k : Key.None;
        return key == Key.None ? label : $"{label} ({KeyDisplayConverter.Format(key)})";
    }

    // ========== 跨组件事件 ==========

    public event Action? NextRequested;
    public event Action? PreviousRequested;
    public event Action? GoBackRequested;
    public event Action? TogglePlaylistRequested;

    // ========== 构造 ==========

    public ControlBarViewModel(IMediaPlayerController media, PlayerInputHandler inputHandler,
                               IThumbnailGenerator thumbnailGenerator)
    {
        _media = media;
        _inputHandler = inputHandler;

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

            // 新视频加载中或 VLC 重 seek 期间：CurrentTime=0 为中间态，抑制避免闪烁
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
            CurrentTimeText = MediaPlayerController.FormatTime(args.CurrentTime);
            TotalTimeText = MediaPlayerController.FormatTime(args.TotalTime);
        };
    }

    // ========== 播放命令 ==========

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

    // ========== 全屏时鼠标控制栏显隐 ==========

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

    // ========== 右键按住 ==========

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

    // ========== 键盘 ==========

    public bool HandleKeyDown(KeyEventArgs e) => _inputHandler.HandleKeyDown(e);

    [RelayCommand]
    private void KeyDown(KeyEventArgs e) => HandleKeyDown(e);

}
