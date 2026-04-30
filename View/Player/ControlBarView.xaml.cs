using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
    private ThumbnailPreviewController? _thumbnailPreviewView;

    private bool _isFullscreen;
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set => _isFullscreen = value;
    }

    // --- Events ---
    public event EventHandler? ControlBarMouseEnter;
    public event EventHandler? ControlBarMouseLeave;

    // --- Setup ---
    public void Setup(PlayerViewModel vm)
    {
        _vm = vm;

        SpeedPopup.PlacementTarget = SpeedBtn;
        SpeedPopup.CustomPopupPlacementCallback = (_, targetSize, _) =>
        {
            double scale = targetSize.Width / SpeedBtn.ActualWidth;
            double pw = 90 * scale;
            double ph = 274 * scale;
            return new[] { new CustomPopupPlacement(
                new Point((targetSize.Width - pw) / 2, -ph),
                PopupPrimaryAxis.Vertical) };
        };

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

    public void CloseSpeedPopup()
        => _vm?.CloseSpeedPopup();

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

    // --- 倍速弹窗（开关状态由 ViewModel.IsSpeedPopupOpen 控制） ---

    private void SpeedBtn_MouseEnter(object sender, MouseEventArgs e)
        => _vm?.OnSpeedEnter();

    private void SpeedBtn_MouseLeave(object sender, MouseEventArgs e)
        => _vm?.OnSpeedLeave();

    private void SpeedPopup_MouseEnter(object sender, MouseEventArgs e)
        => _vm?.OnSpeedEnter();

    private void SpeedPopup_MouseLeave(object sender, MouseEventArgs e)
        => _vm?.OnSpeedLeave();

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
        _thumbnailPreviewView?.Dispose();
    }
}
