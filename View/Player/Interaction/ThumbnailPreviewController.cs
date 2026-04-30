using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LocalPlayer.View.Animations;
using Image = System.Windows.Controls.Image;
using Mouse = System.Windows.Input.Mouse;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace LocalPlayer.View.Player.Interaction;

/// <summary>
/// 进度条悬浮缩略图预览控制器：延迟显隐、鼠标追踪、缩略图加载/缓存、弹窗动画。
/// </summary>
public class ThumbnailPreviewController : IDisposable
{
    private readonly Action<string, Exception?> _logError;
    private readonly Func<string, int, string?> _getThumbnailPath;
    private readonly Func<string, int> _getThumbnailState;
    private readonly Func<long, string> _formatTime;
    private readonly Slider _progressSlider;
    private readonly Popup _progressPopup;
    private readonly Image _thumbnailImage;
    private readonly TextBlock _thumbnailTimeText;
    private readonly Func<long> _getVideoLength;
    private readonly PopupAnimator _animator;

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
        Func<long> getVideoLength,
        Func<string, int, string?> getThumbnailPath,
        Func<string, int> getThumbnailState,
        Func<long, string> formatTime,
        Action<string, Exception?>? logError = null)
    {
        _progressSlider = progressSlider;
        _progressPopup = progressPopup;
        _thumbnailImage = thumbnailImage;
        _thumbnailTimeText = thumbnailTimeText;
        _getVideoLength = getVideoLength;
        _getThumbnailPath = getThumbnailPath;
        _getThumbnailState = getThumbnailState;
        _formatTime = formatTime;
        _logError = logError ?? ((_, _) => { });

        _animator = new PopupAnimator(progressPopupScale, (UIElement)progressPopup.Child!,
            showFromScale: 0.9, showDurationMs: 200,
            hideToScale: 0.9, hideDurationMs: 150);

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

        _thumbnailTimeText.Text = _formatTime(hoverTimeMs);
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
            _getThumbnailState(_currentThumbVideoPath) == 2; // ThumbnailState.Ready = 2
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
        _progressPopup.IsOpen = true;
        _animator.Show();
    }

    private void Hide()
    {
        if (!_isVisible || _isClosing) return;
        _isClosing = true;
        _animator.Hide(() =>
        {
            _progressPopup.IsOpen = false;
            _isVisible = false;
            _isClosing = false;
        });
    }

    // ========== JPEG 加载 ==========

    private BitmapSource? LoadThumbnailJpeg(string videoPath, int second)
    {
        var path = _getThumbnailPath(videoPath, second);
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
            _logError($"缩略图解码异常 second={second}", ex);
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
