using System;
using System.Collections.Generic;
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
    public static void Log(string message) => AppLog.Info(nameof(ControlBarViewModel), message);

    private readonly IMediaPlayerController _media;
    private readonly PlayerInputHandler _inputHandler;

    private float _savedRate = 1.0f;

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

    [RelayCommand]
    private void TogglePlaylist() => TogglePlaylistRequested?.Invoke();

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

    // ========== 播放进度更新（由 PlayerViewModel 驱动） ==========

    public void UpdateProgress(long currentTime, long totalTime)
    {
        CurrentTime = currentTime;
        TotalTime = totalTime;
        CurrentTimeText = MediaPlayerController.FormatTime(currentTime);
        TotalTimeText = MediaPlayerController.FormatTime(totalTime);
    }

    public void UpdateIsPlaying(bool playing) => IsPlaying = playing;
}
