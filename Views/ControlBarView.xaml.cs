using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using LocalPlayer.Helpers;
using LocalPlayer.Services;

// 消歧义：UseWindowsForms 隐式导入与 WPF 类型冲突
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Button = System.Windows.Controls.Button;
using Point = System.Windows.Point;

namespace LocalPlayer.Views;

public partial class ControlBarView : System.Windows.Controls.UserControl, IDisposable
{
    private static void Log(string message) => AppLog.Info(nameof(ControlBarView), message);

    public ControlBarView()
    {
        InitializeComponent();
    }

    private MediaPlayerController? _mediaController;
    private PlayerInputHandler? _inputHandler;
    private SpeedPopupController? _speedPopupController;
    private ThumbnailPreviewController? _thumbnailPreviewController;
    private bool _isProgressDragging;

    public float CurrentSpeed => _speedPopupController?.CurrentSpeed ?? 1.0f;

    private bool _isFullscreen;
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set
        {
            _isFullscreen = value;
            Dispatcher.Invoke(() =>
            {
                FullscreenIcon.Source = new BitmapImage(
                    new Uri(value
                        ? "pack://application:,,,/Resources/Icons/exitFullScreen.png"
                        : "pack://application:,,,/Resources/Icons/fullScreen.png"));
            });
        }
    }

    // --- Events ---
    public event EventHandler? PlayPauseClicked;
    public event EventHandler? PreviousClicked;
    public event EventHandler? NextClicked;
    public event EventHandler? StopClicked;
    public event EventHandler? FullscreenClicked;
    public event EventHandler? PlaylistToggleClicked;
    public event EventHandler? SettingsClicked;
    public event Action<float>? SpeedChanged;
    public event EventHandler? ControlBarMouseEnter;
    public event EventHandler? ControlBarMouseLeave;
    public event Action<long>? SeekRequested;

    // --- Setup ---
    public void Setup(MediaPlayerController mediaController,
                      PlayerInputHandler inputHandler,
                      ThumbnailGenerator thumbnailGenerator)
    {
        _mediaController = mediaController;
        _inputHandler = inputHandler;

        _speedPopupController = new SpeedPopupController(
            SpeedPopup, SpeedBtn, SpeedPopupScale, SpeedOptionsPanel, RootGrid,
            rate => _mediaController.Rate = rate);
        _speedPopupController.SpeedChanged += speed => SpeedChanged?.Invoke(speed);

        _thumbnailPreviewController = new ThumbnailPreviewController(
            ProgressSlider, ProgressPopup, ProgressPopupScale,
            ThumbnailImage, ThumbnailTimeText,
            thumbnailGenerator,
            () => _mediaController.Length);

        _mediaController.Playing += OnPlaying;
        _mediaController.Paused += OnPaused;
        _mediaController.Stopped += OnStopped;
        _mediaController.ProgressUpdated += OnProgressUpdated;

        UpdateButtonTooltips();
    }

    public void SetCurrentVideo(string? videoPath)
        => _thumbnailPreviewController?.SetCurrentVideo(videoPath);

    public void SetSpeed(float speed)
        => _speedPopupController?.SetSpeed(speed);

    public void UpdateSpeedButtonText(float speed)
        => _speedPopupController?.UpdateButtonText(speed);

    public void CloseSpeedPopup()
        => _speedPopupController?.Close();

    public void UpdateButtonTooltips()
    {
        var bindings = _inputHandler?.GetCurrentBindings();
        if (bindings == null) return;

        PlayPauseBtn.ToolTip = $"播放/暂停 ({KeyDisplayString(bindings["TogglePlayPause"])})";
        PreviousBtn.ToolTip = $"上一集 ({KeyDisplayString(bindings["PreviousEpisode"])})";
        NextBtn.ToolTip = $"下一集 ({KeyDisplayString(bindings["NextEpisode"])})";
        FullscreenBtn.ToolTip = $"全屏 ({KeyDisplayString(bindings["ToggleFullscreen"])})";
    }

    // --- MediaController event handlers ---
    private void OnPlaying(object? s, EventArgs e) => Dispatcher.Invoke(() =>
        PlayPauseIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/Icons/pause.png")));

    private void OnPaused(object? s, EventArgs e) => Dispatcher.Invoke(() =>
        PlayPauseIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/Icons/play.png")));

    private void OnStopped(object? s, EventArgs e) => Dispatcher.Invoke(() =>
        PlayPauseIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/Icons/play.png")));

    private void OnProgressUpdated(object? s, ProgressUpdatedEventArgs e)
    {
        if (!_isProgressDragging)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressSlider.Maximum = e.TotalTime;
                ProgressSlider.Value = e.CurrentTime;
                CurrentTimeText.Text = MediaPlayerController.FormatTime(e.CurrentTime);
                TotalTimeText.Text = MediaPlayerController.FormatTime(e.TotalTime);
            });
        }
    }

    // --- Button click handlers ---
    private void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
        => PlayPauseClicked?.Invoke(this, EventArgs.Empty);

    private void PreviousBtn_Click(object sender, RoutedEventArgs e)
        => PreviousClicked?.Invoke(this, EventArgs.Empty);

    private void NextBtn_Click(object sender, RoutedEventArgs e)
        => NextClicked?.Invoke(this, EventArgs.Empty);

    private void StopBtn_Click(object sender, RoutedEventArgs e)
        => StopClicked?.Invoke(this, EventArgs.Empty);

    private void FullscreenBtn_Click(object sender, RoutedEventArgs e)
        => FullscreenClicked?.Invoke(this, EventArgs.Empty);

    private void PlaylistToggleBtn_Click(object sender, RoutedEventArgs e)
        => PlaylistToggleClicked?.Invoke(this, EventArgs.Empty);

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        => SettingsClicked?.Invoke(this, EventArgs.Empty);

    // --- Common button animations ---
    private static readonly CubicBezierEase _btnEase = new()
    {
        X1 = 0.25, Y1 = 0.1, X2 = 0.25, Y2 = 1.0,
        EasingMode = EasingMode.EaseIn
    };
    private static readonly TimeSpan _hoverEnterDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan _hoverExitDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan _pressDuration = TimeSpan.FromMilliseconds(130);
    private static readonly TimeSpan _releaseDuration = TimeSpan.FromMilliseconds(280);

    private void CommonButton_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Button btn) return;
        if (!btn.IsPressed)
            AnimateScale(btn, 1.2, _hoverEnterDuration, _btnEase);
    }

    private void CommonButton_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not Button btn) return;
        if (!btn.IsPressed)
            AnimateScale(btn, 1.0, _hoverExitDuration, _btnEase);
    }

    private void CommonButton_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button btn) return;
        AnimateScale(btn, 0.85, _pressDuration, _btnEase);
    }

    private void CommonButton_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button btn) return;
        double target = btn.IsMouseOver ? 1.2 : 1.0;
        AnimateScale(btn, target, _releaseDuration, _btnEase);
    }

    private static void AnimateScale(Button btn, double target,
        TimeSpan duration, IEasingFunction ease)
    {
        if (btn.Template.FindName("AnimScale", btn) is ScaleTransform st)
        {
            AnimationHelper.AnimateScaleTransform(st, target, (int)duration.TotalMilliseconds, ease);
        }
    }

    // --- Progress slider ---
    private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        if (e.OriginalSource is not Thumb && ProgressSlider.ActualWidth > 0)
        {
            double ratio;
            if (ProgressSlider.Template.FindName("PART_Track", ProgressSlider) is Track track && track.ActualWidth > 0)
            {
                Point trackPos = e.GetPosition(track);
                ratio = Math.Max(0, Math.Min(1, trackPos.X / track.ActualWidth));
            }
            else
            {
                Point pos = e.GetPosition(ProgressSlider);
                ratio = pos.X / ProgressSlider.ActualWidth;
            }
            double newValue = ProgressSlider.Minimum + ratio * (ProgressSlider.Maximum - ProgressSlider.Minimum);
            ProgressSlider.Value = Math.Max(ProgressSlider.Minimum, Math.Min(ProgressSlider.Maximum, newValue));
        }

        _isProgressDragging = true;
    }

    private void ProgressSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        _isProgressDragging = false;
        SeekRequested?.Invoke((long)ProgressSlider.Value);
    }

    private void ProgressSlider_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _isProgressDragging = false;
        SeekRequested?.Invoke((long)ProgressSlider.Value);
    }

    private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        CurrentTimeText.Text = MediaPlayerController.FormatTime((long)ProgressSlider.Value);
    }

    // --- Speed popup forwarding ---
    private void SpeedBtn_MouseEnter(object sender, MouseEventArgs e)
        => _speedPopupController?.OnSpeedBtnMouseEnter();

    private void SpeedBtn_MouseLeave(object sender, MouseEventArgs e)
        => _speedPopupController?.OnSpeedBtnMouseLeave();

    private void SpeedPopup_MouseEnter(object sender, MouseEventArgs e)
        => _speedPopupController?.OnSpeedPopupMouseEnter();

    private void SpeedPopup_MouseLeave(object sender, MouseEventArgs e)
        => _speedPopupController?.OnSpeedPopupMouseLeave();

    private void SpeedOption_Click(object sender, RoutedEventArgs e)
        => _speedPopupController?.OnSpeedOptionClick(sender);

    // --- Thumbnail preview forwarding ---
    private void ProgressSlider_MouseEnter(object sender, MouseEventArgs e)
        => _thumbnailPreviewController?.OnSliderMouseEnter();

    private void ProgressSlider_MouseLeave(object sender, MouseEventArgs e)
        => _thumbnailPreviewController?.OnSliderMouseLeave();

    private void ProgressSlider_MouseMove(object sender, MouseEventArgs e)
        => _thumbnailPreviewController?.OnSliderMouseMove(e);

    private void ProgressPopup_MouseEnter(object sender, MouseEventArgs e)
        => _thumbnailPreviewController?.OnPopupMouseEnter();

    private void ProgressPopup_MouseLeave(object sender, MouseEventArgs e)
        => _thumbnailPreviewController?.OnPopupMouseLeave();

    // --- Mouse enter/leave on root grid (for fullscreen auto-hide) ---
    private void RootGrid_MouseEnter(object sender, MouseEventArgs e)
        => ControlBarMouseEnter?.Invoke(this, EventArgs.Empty);

    private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
        => ControlBarMouseLeave?.Invoke(this, EventArgs.Empty);

    // --- Keyboard forwarding ---
    private void RootGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        Log($"PreviewKeyDown: Key={e.Key}");
        if (_inputHandler?.HandleKeyDown(e, _isFullscreen) == true)
            e.Handled = true;
    }

    private void RootGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        Log($"KeyDown: Key={e.Key}");
        if (_inputHandler?.HandleKeyDown(e, _isFullscreen) == true)
            e.Handled = true;
    }

    // --- Key display helper ---
    private static string KeyDisplayString(Key key)
    {
        return key.ToString()
            .Replace("Left", "←").Replace("Right", "→")
            .Replace("Space", "空格").Replace("Escape", "Esc")
            .Replace("PageUp", "PgUp").Replace("PageDown", "PgDn")
            .Replace("Return", "Enter");
    }

    public void Dispose()
    {
        if (_mediaController != null)
        {
            _mediaController.Playing -= OnPlaying;
            _mediaController.Paused -= OnPaused;
            _mediaController.Stopped -= OnStopped;
            _mediaController.ProgressUpdated -= OnProgressUpdated;
        }
        _speedPopupController?.Dispose();
        _thumbnailPreviewController?.Dispose();
    }
}
