using System;
using System.Windows.Input;
using System.Windows.Threading;
using LocalPlayer.Media;


namespace LocalPlayer.Interaction;

/// <summary>
/// 右键长按倍速控制器：按住右键 350ms 后切换到指定倍速，松手恢复。
/// PlayerPage 和 FullscreenWindow 共用。
/// </summary>
public class RightHoldSpeedController
{
    private readonly MediaPlayerController _mediaCtrl;
    private readonly Func<float> _getCurrentSpeed;
    private readonly Action<float> _onSpeedChanged;
    private readonly float _holdSpeed;
    private readonly DispatcherTimer _timer;

    private float _savedSpeed;
    private bool _isHolding;

    public RightHoldSpeedController(
        MediaPlayerController mediaCtrl,
        Func<float> getCurrentSpeed,
        Action<float> onSpeedChanged,
        float holdSpeed = 3.0f)
    {
        _mediaCtrl = mediaCtrl;
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
            _mediaCtrl.Rate = _savedSpeed;
            _onSpeedChanged(_savedSpeed);
        }
        e.Handled = true;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        _savedSpeed = _getCurrentSpeed();
        _isHolding = true;
        _mediaCtrl.Rate = _holdSpeed;
        _onSpeedChanged(_holdSpeed);
    }
}
