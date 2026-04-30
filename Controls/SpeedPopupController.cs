using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LocalPlayer.Primitives;
using LocalPlayer.Model;
using Button = System.Windows.Controls.Button;
using Panel = System.Windows.Controls.Panel;
using Mouse = System.Windows.Input.Mouse;
using Point = System.Windows.Point;

namespace LocalPlayer.Controls;

/// <summary>
/// 倍速弹窗控制器：延迟显隐、动画、倍速选择。
/// </summary>
public class SpeedPopupController : IDisposable
{
    private static void Log(string message) => AppLog.Info(nameof(SpeedPopupController), message);

    private readonly Popup _speedPopup;
    private readonly Button _speedBtn;
    private readonly ScaleTransform _speedPopupScale;
    private readonly Panel _speedOptionsPanel;
    private readonly UIElement _pageRoot;
    private readonly Action<float> _setRate;

    private readonly DispatcherTimer _closeTimer;

    private float _currentSpeed = 1.0f;
    private bool _isClosing;

    public float CurrentSpeed => _currentSpeed;

    public event Action<float>? SpeedChanged;

    public SpeedPopupController(
        Popup speedPopup,
        Button speedBtn,
        ScaleTransform speedPopupScale,
        Panel speedOptionsPanel,
        UIElement pageRoot,
        Action<float> setRate)
    {
        _speedPopup = speedPopup;
        _speedBtn = speedBtn;
        _speedPopupScale = speedPopupScale;
        _speedOptionsPanel = speedOptionsPanel;
        _pageRoot = pageRoot;
        _setRate = setRate;

        _speedPopup.CustomPopupPlacementCallback = (_, targetSize, _) =>
        {
            double scale = targetSize.Width / _speedBtn.ActualWidth;
            double pw = 90 * scale;
            double ph = 274 * scale;
            double x = (targetSize.Width - pw) / 2;
            double y = -ph - 10 * scale;
            return new[] { new CustomPopupPlacement(
                new Point(x, y),
                PopupPrimaryAxis.Vertical) };
        };
        _speedPopup.PlacementTarget = _speedBtn;

        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _closeTimer.Tick += OnCloseTimerTick;
    }

    // ========== 鼠标事件转发 ==========

    public void OnSpeedBtnMouseEnter()
    {
        _closeTimer.Stop();

        if (_isClosing)
        {
            _speedPopup.IsOpen = true;
            AnimateIn();
            return;
        }

        bool wasClosed = !_speedPopup.IsOpen;
        _speedPopup.IsOpen = true;
        if (wasClosed)
        {
            var dispatcher = _speedBtn.Dispatcher;
            dispatcher.BeginInvoke(() =>
            {
                HighlightSpeedOption(_currentSpeed);
                AnimateIn();
            }, DispatcherPriority.Loaded);
        }
    }

    public void OnSpeedBtnMouseLeave()
    {
        _closeTimer.Stop();
        _closeTimer.Start();
    }

    public void OnSpeedPopupMouseEnter()
    {
        _closeTimer.Stop();
        if (_isClosing)
        {
            _speedPopup.IsOpen = true;
            AnimateIn();
        }
    }

    public void OnSpeedPopupMouseLeave()
    {
        _closeTimer.Stop();
        _closeTimer.Start();
    }

    public void OnPageRootPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!_speedPopup.IsOpen || _speedPopup.Child == null) return;

        if (e.OriginalSource is DependencyObject dep && IsDescendantOf(dep, _speedPopup.Child))
            return;

        var pos = e.GetPosition(_pageRoot);
        var btnBounds = _speedBtn.TransformToAncestor(_pageRoot).TransformBounds(
            new Rect(0, 0, _speedBtn.ActualWidth, _speedBtn.ActualHeight));
        if (!btnBounds.Contains(pos))
            AnimateOut();
    }

    // ========== 倍速选项点击 ==========

    public void OnSpeedOptionClick(object sender)
    {
        if (sender is Button btn && btn.Tag is string tagStr &&
            float.TryParse(tagStr, out float speed))
        {
            SetSpeed(speed);
        }
    }

    public void SetSpeed(float speed)
    {
        _currentSpeed = speed;
        _setRate(speed);
        _speedBtn.Content = $"{speed:0.##}x";
        if (_speedPopup.IsOpen)
            HighlightSpeedOption(speed);
        SpeedChanged?.Invoke(speed);
    }

    public void UpdateButtonText(float speed)
    {
        _currentSpeed = speed;
        _speedBtn.Content = $"{speed:0.##}x";
    }

    // ========== 关闭弹窗 ==========

    public void Close()
    {
        _closeTimer.Stop();
        if (_speedPopup.IsOpen)
            AnimateOut();
    }

    // ========== 动画 ==========

    private void AnimateIn()
    {
        _isClosing = false;
        var border = _speedPopup.Child as Border;
        if (border == null) return;

        _speedPopupScale.ScaleX = 0;
        _speedPopupScale.ScaleY = 0;
        border.Opacity = 0;

        AnimationHelper.AnimateScaleTransform(_speedPopupScale, 1, 250, AnimationHelper.EaseOut);
        AnimationHelper.Animate(border, UIElement.OpacityProperty, 0, 1, 250, AnimationHelper.EaseOut);
    }

    private void AnimateOut()
    {
        if (_isClosing) return;
        _isClosing = true;

        var border = _speedPopup.Child as Border;
        if (border == null)
        {
            _speedPopup.IsOpen = false;
            _isClosing = false;
            return;
        }

        AnimationHelper.AnimateScaleTransform(_speedPopupScale, 0, 180, AnimationHelper.EaseIn);
        AnimationHelper.AnimateFromCurrent(border, UIElement.OpacityProperty, 0, 180, AnimationHelper.EaseIn, () =>
        {
            _speedPopup.IsOpen = false;
            _isClosing = false;
        });
    }

    private void OnCloseTimerTick(object? sender, EventArgs e)
    {
        _closeTimer.Stop();

        if (IsMouseOverSafeZone())
        {
            _closeTimer.Start();
            return;
        }

        AnimateOut();
    }

    private bool IsMouseOverSafeZone()
    {
        var btnPt = Mouse.GetPosition(_speedBtn);
        if (btnPt.X >= -2 && btnPt.Y >= -2 &&
            btnPt.X <= _speedBtn.ActualWidth + 2 && btnPt.Y <= _speedBtn.ActualHeight + 2)
            return true;

        if (_speedPopup.IsOpen && _speedPopup.Child != null)
        {
            try
            {
                var popupPt = Mouse.GetPosition(_speedPopup.Child);
                if (popupPt.X >= -2 && popupPt.Y >= -2 &&
                    popupPt.X <= _speedPopup.Child.RenderSize.Width + 2 &&
                    popupPt.Y <= _speedPopup.Child.RenderSize.Height + 2)
                    return true;
            }
            catch { }
        }

        return false;
    }

    // ========== 高亮当前倍速 ==========

    private void HighlightSpeedOption(float speed)
    {
        var selectedColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#007AFF");
        var duration = TimeSpan.FromMilliseconds(300);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        foreach (var child in _speedOptionsPanel.Children)
        {
            if (child is not Button btn) continue;

            bool isSelected = btn.Tag?.ToString() == speed.ToString("0.##");
            var targetColor = isSelected ? selectedColor : Colors.Transparent;
            var targetSize = isSelected ? 14.0 : 13.0;

            if (btn.Template.FindName("BgBrush", btn) is SolidColorBrush brush)
            {
                brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                brush.BeginAnimation(SolidColorBrush.ColorProperty,
                    new ColorAnimation(targetColor, duration) { EasingFunction = ease });
            }

            btn.BeginAnimation(Button.FontSizeProperty, null);
            btn.BeginAnimation(Button.FontSizeProperty,
                new DoubleAnimation(targetSize, duration) { EasingFunction = ease });

            btn.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    // ========== 工具 ==========

    private static bool IsDescendantOf(DependencyObject? dep, DependencyObject ancestor)
    {
        while (dep != null)
        {
            if (dep == ancestor) return true;
            dep = VisualTreeHelper.GetParent(dep);
        }
        return false;
    }

    public void Dispose()
    {
        _closeTimer.Stop();
    }
}
