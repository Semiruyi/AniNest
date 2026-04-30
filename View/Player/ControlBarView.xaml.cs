using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LocalPlayer.View.Animations;
using LocalPlayer.View.Player.Interaction;
using LocalPlayer.ViewModel;

namespace LocalPlayer.View.Player;

public partial class ControlBarView : System.Windows.Controls.UserControl, IDisposable
{
    public ControlBarView()
    {
        InitializeComponent();
    }

    private PlayerViewModel? _vm;
    private SpeedPopupController? _speedPopupView;
    private ThumbnailPreviewController? _thumbnailPreviewView;

    public float CurrentSpeed => _speedPopupView?.CurrentSpeed ?? 1.0f;

    private bool _isFullscreen;
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set => _isFullscreen = value;
    }

    // --- Events ---
    public event Action<float>? SpeedChanged;
    public event EventHandler? ControlBarMouseEnter;
    public event EventHandler? ControlBarMouseLeave;

    // --- Setup ---
    public void Setup(PlayerViewModel vm)
    {
        _vm = vm;

        var border = SpeedPopup.Child as Border;
        _speedPopupView = new SpeedPopupController(
            SpeedPopup, SpeedBtn, SpeedPopupScale, SpeedOptionsPanel, RootGrid,
            rate => _vm.SetRate(rate));

        _speedPopupView.SpeedChanged += speed => SpeedChanged?.Invoke(speed);

        _thumbnailPreviewView = new ThumbnailPreviewController(
            ProgressSlider, ProgressPopup, ProgressPopupScale,
            ThumbnailImage, ThumbnailTimeText,
            () => _vm.MediaLength,
            (path, second) => _vm.GetThumbnailPath(path, second),
            path => (int)_vm.GetThumbnailState(path),
            ms => PlayerViewModel.FormatTime(ms));

        WireButtonAnimations();
        UpdateButtonTooltips();
    }

    private void WireButtonAnimations()
    {
        foreach (var btn in new[] { PlayPauseBtn, PreviousBtn, NextBtn, FullscreenBtn })
        {
            if (btn.Template.FindName("AnimScale", btn) is ScaleTransform st)
                ButtonScaleHover.Attach(btn, st);
        }
    }

    public void SetCurrentVideo(string? videoPath)
        => _thumbnailPreviewView?.SetCurrentVideo(videoPath);

    public void SetSpeed(float speed)
        => _speedPopupView?.SetSpeed(speed);

    public void UpdateSpeedButtonText(float speed)
        => _speedPopupView?.UpdateButtonText(speed);

    public void CloseSpeedPopup()
        => _speedPopupView?.Close();

    public void UpdateButtonTooltips()
    {
        if (_vm == null) return;
        var bindings = _vm.GetCurrentBindings();
        if (bindings == null) return;

        PlayPauseBtn.ToolTip = $"播放/暂停 ({KeyDisplayString(bindings["TogglePlayPause"])})";
        PreviousBtn.ToolTip = $"上一集 ({KeyDisplayString(bindings["PreviousEpisode"])})";
        NextBtn.ToolTip = $"下一集 ({KeyDisplayString(bindings["NextEpisode"])})";
        FullscreenBtn.ToolTip = $"全屏 ({KeyDisplayString(bindings["ToggleFullscreen"])})";
    }

    // --- Progress slider ---
    private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (DataContext is PlayerViewModel vm)
            vm.IsSeeking = true;

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
    }

    private void ProgressSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (DataContext is PlayerViewModel vm)
        {
            vm.IsSeeking = false;
            vm.SeekCommand.Execute((long)ProgressSlider.Value);
        }
    }

    private void ProgressSlider_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (DataContext is PlayerViewModel vm)
        {
            vm.IsSeeking = false;
            vm.SeekCommand.Execute((long)ProgressSlider.Value);
        }
    }

    // --- Speed popup forwarding ---
    private void SpeedBtn_MouseEnter(object sender, MouseEventArgs e)
        => _speedPopupView?.OnSpeedBtnMouseEnter();

    private void SpeedBtn_MouseLeave(object sender, MouseEventArgs e)
        => _speedPopupView?.OnSpeedBtnMouseLeave();

    private void SpeedPopup_MouseEnter(object sender, MouseEventArgs e)
        => _speedPopupView?.OnSpeedPopupMouseEnter();

    private void SpeedPopup_MouseLeave(object sender, MouseEventArgs e)
        => _speedPopupView?.OnSpeedPopupMouseLeave();

    private void SpeedOption_Click(object sender, RoutedEventArgs e)
        => _speedPopupView?.OnSpeedOptionClick(sender);

    // --- Thumbnail preview forwarding ---
    private void ProgressSlider_MouseEnter(object sender, MouseEventArgs e)
        => _thumbnailPreviewView?.OnSliderMouseEnter();

    private void ProgressSlider_MouseLeave(object sender, MouseEventArgs e)
        => _thumbnailPreviewView?.OnSliderMouseLeave();

    private void ProgressSlider_MouseMove(object sender, MouseEventArgs e)
        => _thumbnailPreviewView?.OnSliderMouseMove(e);

    private void ProgressPopup_MouseEnter(object sender, MouseEventArgs e)
        => _thumbnailPreviewView?.OnPopupMouseEnter();

    private void ProgressPopup_MouseLeave(object sender, MouseEventArgs e)
        => _thumbnailPreviewView?.OnPopupMouseLeave();

    // --- Mouse enter/leave on root grid (for fullscreen auto-hide) ---
    private void RootGrid_MouseEnter(object sender, MouseEventArgs e)
        => ControlBarMouseEnter?.Invoke(this, EventArgs.Empty);

    private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
        => ControlBarMouseLeave?.Invoke(this, EventArgs.Empty);

    // --- Keyboard forwarding ---
    private void RootGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm?.HandleKeyDown(e, _isFullscreen) == true)
            e.Handled = true;
    }

    private void RootGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (_vm?.HandleKeyDown(e, _isFullscreen) == true)
            e.Handled = true;
    }

    // --- Key display helper ---
    private static readonly Converters.KeyDisplayConverter _keyConverter = new();

    private static string KeyDisplayString(Key key)
        => (string)_keyConverter.Convert(key, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);

    public void Dispose()
    {
        _speedPopupView?.Dispose();
        _thumbnailPreviewView?.Dispose();
    }
}
