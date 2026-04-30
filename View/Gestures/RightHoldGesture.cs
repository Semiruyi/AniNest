using System;
using System.Windows.Input;
using System.Windows.Threading;

namespace LocalPlayer.View.Gestures;

/// <summary>
/// 右键长按手势识别：按下超过阈值时间触发 HoldStarted，松手触发 HoldEnded。
/// </summary>
public class RightHoldGesture : IDisposable
{
    private readonly DispatcherTimer _timer;
    private bool _isHolding;

    public event Action? HoldStarted;
    public event Action? HoldEnded;

    public RightHoldGesture(int holdDurationMs = 350)
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(holdDurationMs) };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            _isHolding = true;
            HoldStarted?.Invoke();
        };
    }

    public void OnMouseDown(MouseButtonEventArgs e)
    {
        _timer.Stop();
        _timer.Start();
        e.Handled = true;
    }

    public void OnMouseUp(MouseButtonEventArgs e)
    {
        _timer.Stop();
        if (_isHolding)
        {
            _isHolding = false;
            HoldEnded?.Invoke();
        }
        e.Handled = true;
    }

    public void Dispose()
    {
        _timer.Stop();
        if (_isHolding)
        {
            _isHolding = false;
            HoldEnded?.Invoke();
        }
    }
}
