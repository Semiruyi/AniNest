using System;
using System.Windows.Input;
using System.Windows.Threading;

namespace LocalPlayer.View.Player.Interaction;

/// <summary>
/// 右键长按倍速控制器：按住右键 350ms 后切换到指定倍速，松手恢复。
/// PlayerPage 和 FullscreenWindow 共用。
/// </summary>
public class RightHoldSpeedController
{
    private readonly Action<float> _setRate;
    private readonly Func<float> _getCurrentSpeed;
    private readonly Action<float> _onSpeedChanged;
    private readonly float _holdSpeed;
    private readonly DispatcherTimer _timer;

    private float _savedSpeed;
    private bool _isHolding;

    public RightHoldSpeedController(
        Action<float> setRate,
        Func<float> getCurrentSpeed,
        Action<float> onSpeedChanged,
        float holdSpeed = 3.0f)
    {
        _setRate = setRate;
        _getCurrentSpeed = getCurrentSpeed;
        _onSpeedChanged = onSpeedChanged;
        _holdSpeed = holdSpeed;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _timer.Tick += OnTimerTick;
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
            _setRate(_savedSpeed);
            _onSpeedChanged(_savedSpeed);
        }
        e.Handled = true;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        _savedSpeed = _getCurrentSpeed();
        _isHolding = true;
        _setRate(_holdSpeed);
        _onSpeedChanged(_holdSpeed);
    }

    public void Dispose()
    {
        _timer.Stop();
        if (_isHolding)
        {
            _isHolding = false;
            _setRate(_savedSpeed);
        }
    }
}
