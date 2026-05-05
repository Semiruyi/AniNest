using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LocalPlayer.Model;

namespace LocalPlayer.View.Diagnostics;

/// <summary>
/// FPS / 帧时间 / 渲染模式 监控叠加层，默认 Ctrl+Shift+F 开关。
/// </summary>
public class FpsMonitor
{
    private static readonly Logger Log = AppLog.For(nameof(FpsMonitor));

    private readonly Window _window;
    private readonly Border _overlay;
    private readonly TextBlock _text;
    private readonly DispatcherTimer _updateTimer;
    private readonly DispatcherTimer _logTimer;

    private DateTime _lastFrame = DateTime.UtcNow;
    private int _frameCount;
    private double _currentFps;
    private double _minFps = double.MaxValue;
    private double _maxFps;
    private double _frameTimeMs;
    private int _renderTier;
    private int _sampleCount;
    private double _fpsSum;
    private DateTime _lastScreenUpdate = DateTime.UtcNow;
    private DateTime _lastLogTime = DateTime.UtcNow;
    private bool _visible;

    public FpsMonitor(Window window)
    {
        _window = window;

        _text = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 128)),
            TextAlignment = TextAlignment.Right,
            LineHeight = 15,
        };

        _overlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xAA, 0, 0, 0)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 6, 6, 0),
            Child = _text,
            Visibility = Visibility.Collapsed,
        };

        _updateTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.Normal,
            (_, _) => UpdateDisplay(),
            _window.Dispatcher);

        _logTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Background,
            (_, _) => LogFps(),
            _window.Dispatcher);

        _renderTier = RenderCapability.Tier >> 16;
        _window.KeyDown += OnKeyDown;

        CompositionTarget.Rendering += OnRendering;
    }

    public void Attach()
    {
        if (_window.Content is Grid rootGrid)
        {
            Panel.SetZIndex(_overlay, int.MaxValue);
            rootGrid.Children.Add(_overlay);
        }
        else if (_window.Content is UIElement root)
        {
            var wrap = new Grid();
            _window.Content = null;
            wrap.Children.Add(root);
            Panel.SetZIndex(_overlay, int.MaxValue);
            wrap.Children.Add(_overlay);
            _window.Content = wrap;
        }
    }

    public void Show()
    {
        _visible = true;
        _overlay.Visibility = Visibility.Visible;
        _minFps = double.MaxValue;
        _maxFps = 0;
        _sampleCount = 0;
        _fpsSum = 0;
        _lastLogTime = DateTime.UtcNow;
        _updateTimer.Start();
        _logTimer.Start();
        Log.Info("FPS 监控开启");
    }

    public void Hide()
    {
        _visible = false;
        _overlay.Visibility = Visibility.Collapsed;
        _updateTimer.Stop();
        _logTimer.Stop();
        Log.Info($"FPS 监控关闭 | Min={_minFps:F1} Max={_maxFps:F1} RenderTier={_renderTier}");
    }

    public void Toggle()
    {
        if (_visible) Hide(); else Show();
    }

    public void Detach()
    {
        _updateTimer.Stop();
        _logTimer.Stop();
        CompositionTarget.Rendering -= OnRendering;
        _window.KeyDown -= OnKeyDown;
        if (_overlay.Parent is Panel p)
            p.Children.Remove(_overlay);
    }

    private void LogFps()
    {
        if (!_visible) return;

        var level = _currentFps < 60 ? LogLevel.Warning : LogLevel.Debug;
        AppLog.Write("player.log", nameof(FpsMonitor), level,
            $"FPS={_currentFps:F1} Frame={_frameTimeMs:F2}ms Min={_minFps:F1} Max={_maxFps:F1} Tier={_renderTier}");
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var delta = (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        _frameCount++;

        _frameTimeMs = delta * 1000.0;
        _renderTier = RenderCapability.Tier >> 16;

        if (!_visible) return;

        _fpsSum += delta > 0 ? 1.0 / delta : 0;
        _sampleCount++;

        // 250ms 更新一次屏幕显示
        if ((now - _lastScreenUpdate).TotalMilliseconds >= 250)
        {
            _currentFps = _fpsSum / _sampleCount;
            _fpsSum = 0;
            _sampleCount = 0;
            _lastScreenUpdate = now;

            if (_currentFps < _minFps) _minFps = _currentFps;
            if (_currentFps > _maxFps) _maxFps = _currentFps;
        }
    }

    private void UpdateDisplay()
    {
        if (!_visible) return;

        var tierText = _renderTier switch
        {
            2 => "HW Tier 2 (全硬件)",
            1 => "HW Tier 1 (部分硬件)",
            _ => "SW Tier 0 (软件渲染)"
        };

        var fpsColor = _currentFps switch
        {
            >= 120 => "#00FF80",
            >= 60 => "#FFD700",
            _ => "#FF4444"
        };

        _text.Text =
            $"FPS   {_currentFps,6:F1}  <Span Foreground=\"{fpsColor}\">●</Span>\n" +
            $"Min   {_minFps,6:F1}\n" +
            $"Max   {_maxFps,6:F1}\n" +
            $"Frame {_frameTimeMs,6:F2} ms\n" +
            $"      {tierText}";
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            Toggle();
    }
}
