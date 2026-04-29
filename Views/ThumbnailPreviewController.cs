using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LocalPlayer.Helpers;
using LocalPlayer.Services;
using Image = System.Windows.Controls.Image;
using Mouse = System.Windows.Input.Mouse;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace LocalPlayer.Views;

/// <summary>
/// 进度条悬浮缩略图预览控制器：延迟显隐、鼠标追踪、缩略图加载/缓存、弹窗动画。
/// </summary>
public class ThumbnailPreviewController : IDisposable
{
    private static void LogError(string message, Exception? ex = null)
        => AppLog.Error(nameof(ThumbnailPreviewController), message, ex);

    private readonly Slider _progressSlider;
    private readonly Popup _progressPopup;
    private readonly ScaleTransform _progressPopupScale;
    private readonly Image _thumbnailImage;
    private readonly TextBlock _thumbnailTimeText;
    private readonly ThumbnailGenerator _thumbnailGenerator;
    private readonly Func<long> _getVideoLength;

    private readonly DispatcherTimer _showTimer;
    private readonly DispatcherTimer _hideTimer;
    private readonly Dictionary<int, BitmapSource> _thumbnailCache = new();

    private bool _isHovering;
    private bool _isVisible;
    private bool _isClosing;
    private int _lastRequestedSecond = -1;
    private string? _currentThumbVideoPath;

    public ThumbnailPreviewController(
        Slider progressSlider,
        Popup progressPopup,
        ScaleTransform progressPopupScale,
        Image thumbnailImage,
        TextBlock thumbnailTimeText,
        ThumbnailGenerator thumbnailGenerator,
        Func<long> getVideoLength)
    {
        _progressSlider = progressSlider;
        _progressPopup = progressPopup;
        _progressPopupScale = progressPopupScale;
        _thumbnailImage = thumbnailImage;
        _thumbnailTimeText = thumbnailTimeText;
        _thumbnailGenerator = thumbnailGenerator;
        _getVideoLength = getVideoLength;

        _showTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _showTimer.Tick += OnShowTimerTick;

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _hideTimer.Tick += OnHideTimerTick;

        _progressPopup.PlacementTarget = _progressSlider;
    }

    // ========== 当前视频 ==========

    public void SetCurrentVideo(string? videoPath)
    {
        _thumbnailCache.Clear();
        _lastRequestedSecond = -1;
        _currentThumbVideoPath = videoPath;
    }

    // ========== 鼠标事件转发 ==========

    public void OnSliderMouseEnter()
    {
        _isHovering = true;
        _hideTimer.Stop();
        if (!_isVisible)
            _showTimer.Start();
    }

    public void OnSliderMouseLeave()
    {
        _isHovering = false;
        _showTimer.Stop();
        _hideTimer.Start();
    }

    public void OnSliderMouseMove(MouseEventArgs e)
    {
        long length = _getVideoLength();
        if (length <= 0) return;

        var pos = e.GetPosition(_progressSlider);
        double ratio = Math.Max(0, Math.Min(1, pos.X / _progressSlider.ActualWidth));
        long hoverTimeMs = (long)(ratio * length);
        int hoverSecond = (int)(hoverTimeMs / 1000);

        _thumbnailTimeText.Text = MediaPlayerController.FormatTime(hoverTimeMs);
        double popupW = 160;
        double offsetX = Math.Max(0, Math.Min(pos.X - popupW / 2, _progressSlider.ActualWidth - popupW));
        _progressPopup.HorizontalOffset = offsetX;
        _progressPopup.VerticalOffset = -90 - 30;

        if (_isVisible && _progressPopup.IsOpen)
        {
            _progressPopup.HorizontalOffset = offsetX + 1;
            _progressPopup.HorizontalOffset = offsetX;
        }

        bool thumbReady = _currentThumbVideoPath != null &&
            _thumbnailGenerator.GetState(_currentThumbVideoPath) == ThumbnailState.Ready;
        _thumbnailImage.Visibility = thumbReady ? Visibility.Visible : Visibility.Collapsed;

        if (hoverSecond == _lastRequestedSecond) return;
        _lastRequestedSecond = hoverSecond;

        if (thumbReady && _currentThumbVideoPath != null)
        {
            if (_thumbnailCache.TryGetValue(hoverSecond, out var cached))
            {
                _thumbnailImage.Source = cached;
            }
            else
            {
                var bmp = LoadThumbnailJpeg(_currentThumbVideoPath, hoverSecond);
                if (bmp != null)
                {
                    _thumbnailCache[hoverSecond] = bmp;
                    _thumbnailImage.Source = bmp;

                    if (_thumbnailCache.Count > 20)
                    {
                        var toRemove = _thumbnailCache.Keys.OrderBy(k => k).Take(_thumbnailCache.Count / 2).ToList();
                        foreach (var k in toRemove) _thumbnailCache.Remove(k);
                    }
                }
            }
        }
    }

    public void OnPopupMouseEnter()
    {
        _hideTimer.Stop();
    }

    public void OnPopupMouseLeave()
    {
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    // ========== 定时器 ==========

    private void OnShowTimerTick(object? sender, EventArgs e)
    {
        _showTimer.Stop();
        if (!_isHovering) return;
        Show();
    }

    private void OnHideTimerTick(object? sender, EventArgs e)
    {
        _hideTimer.Stop();
        if (_isHovering) return;

        if (_progressPopup.IsOpen && _progressPopup.Child != null)
        {
            try
            {
                var pt = Mouse.GetPosition(_progressPopup.Child);
                if (pt.X >= -2 && pt.Y >= -2 &&
                    pt.X <= _progressPopup.Child.RenderSize.Width + 2 &&
                    pt.Y <= _progressPopup.Child.RenderSize.Height + 2)
                {
                    _hideTimer.Start();
                    return;
                }
            }
            catch { }
        }

        Hide();
    }

    // ========== 动画 ==========

    private void Show()
    {
        if (_isVisible || _isClosing) return;
        _isVisible = true;
        var border = _progressPopup.Child as Border;
        if (border == null) return;

        _progressPopupScale.ScaleX = 0.9;
        _progressPopupScale.ScaleY = 0.9;
        border.Opacity = 0;
        _progressPopup.IsOpen = true;

        AnimationHelper.AnimateScaleTransform(_progressPopupScale, 1, 200, AnimationHelper.EaseOut);
        AnimationHelper.Animate(border, UIElement.OpacityProperty, 0, 1, 200, AnimationHelper.EaseOut);
    }

    private void Hide()
    {
        if (!_isVisible || _isClosing) return;
        _isClosing = true;
        var border = _progressPopup.Child as Border;
        if (border == null)
        {
            _progressPopup.IsOpen = false;
            _isVisible = false;
            _isClosing = false;
            return;
        }

        AnimationHelper.AnimateScaleTransform(_progressPopupScale, 0.9, 150, AnimationHelper.EaseIn);
        AnimationHelper.AnimateFromCurrent(border, UIElement.OpacityProperty, 0, 150, AnimationHelper.EaseIn, () =>
        {
            _progressPopup.IsOpen = false;
            _isVisible = false;
            _isClosing = false;
        });
    }

    // ========== JPEG 加载 ==========

    private BitmapSource? LoadThumbnailJpeg(string videoPath, int second)
    {
        var path = _thumbnailGenerator.GetThumbnailPath(videoPath, second);
        if (path == null) return null;
        try
        {
            var decoder = new JpegBitmapDecoder(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch (Exception ex)
        {
            LogError($"缩略图解码异常 second={second}", ex);
            return null;
        }
    }

    public void Dispose()
    {
        _showTimer.Stop();
        _hideTimer.Stop();
        _thumbnailCache.Clear();
    }
}
