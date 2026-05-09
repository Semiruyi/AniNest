using System;
using System.Windows.Threading;

namespace AniNest.Presentation.Behaviors;

public sealed class HoverPopupController : IDisposable
{
    private readonly DispatcherTimer _openTimer;
    private readonly DispatcherTimer _closeTimer;
    private readonly Func<bool> _getIsOpen;
    private readonly Action<bool> _setIsOpen;
    private bool _isHoveringHost;
    private bool _isHoveringPopup;

    public HoverPopupController(
        HoverPopupTiming timing,
        Func<bool> getIsOpen,
        Action<bool> setIsOpen)
    {
        _getIsOpen = getIsOpen;
        _setIsOpen = setIsOpen;
        _openTimer = CreateTimer(timing.OpenDelay, OpenIfHovered);
        _closeTimer = CreateTimer(timing.CloseDelay, CloseIfIdle);
    }

    public void OnHostEnter()
    {
        _isHoveringHost = true;
        _closeTimer.Stop();

        if (_getIsOpen())
            return;

        RestartTimer(_openTimer);
    }

    public void OnHostLeave()
    {
        _isHoveringHost = false;
        _openTimer.Stop();

        if (_isHoveringPopup)
            return;

        RestartTimer(_closeTimer);
    }

    public void OnPopupEnter()
    {
        _isHoveringPopup = true;
        _closeTimer.Stop();
    }

    public void OnPopupLeave()
    {
        _isHoveringPopup = false;

        if (_isHoveringHost)
            return;

        RestartTimer(_closeTimer);
    }

    public void CloseNow()
    {
        _openTimer.Stop();
        _closeTimer.Stop();
        _isHoveringHost = false;
        _isHoveringPopup = false;
        _setIsOpen(false);
    }

    public void Dispose()
    {
        _openTimer.Stop();
        _closeTimer.Stop();
    }

    private static DispatcherTimer CreateTimer(TimeSpan interval, Action callback)
    {
        var timer = new DispatcherTimer
        {
            Interval = interval
        };

        timer.Tick += (_, _) =>
        {
            timer.Stop();
            callback();
        };

        return timer;
    }

    private static void RestartTimer(DispatcherTimer timer)
    {
        timer.Stop();
        timer.Start();
    }

    private void OpenIfHovered()
    {
        if (_isHoveringHost || _isHoveringPopup)
            _setIsOpen(true);
    }

    private void CloseIfIdle()
    {
        if (!_isHoveringHost && !_isHoveringPopup)
            _setIsOpen(false);
    }
}
