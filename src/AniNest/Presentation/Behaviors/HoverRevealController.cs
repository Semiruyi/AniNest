using System;
using System.Windows.Threading;

namespace AniNest.Presentation.Behaviors;

public sealed class HoverRevealController : IDisposable
{
    private readonly DispatcherTimer _showTimer;
    private readonly DispatcherTimer _hideTimer;
    private readonly Func<bool> _getIsActive;
    private readonly Action<bool> _setIsActive;
    private HoverRevealTiming _timing;
    private bool _isPointerInside;
    private DateTime _visibleSinceUtc = DateTime.MinValue;

    public HoverRevealController(
        HoverRevealTiming timing,
        Func<bool> getIsActive,
        Action<bool> setIsActive)
    {
        _timing = timing;
        _getIsActive = getIsActive;
        _setIsActive = setIsActive;
        _showTimer = CreateTimer(ShowIfHovered);
        _hideTimer = CreateTimer(HideIfIdle);
    }

    public void UpdateTiming(HoverRevealTiming timing)
    {
        _timing = timing;

        if (_hideTimer.IsEnabled && _getIsActive())
            ScheduleHide();
    }

    public void OnPointerEnter()
    {
        _isPointerInside = true;
        _hideTimer.Stop();

        if (_getIsActive())
            return;

        ScheduleShow();
    }

    public void OnPointerLeave()
    {
        _isPointerInside = false;
        _showTimer.Stop();

        if (!_getIsActive())
            return;

        ScheduleHide();
    }

    public void Reset()
    {
        _showTimer.Stop();
        _hideTimer.Stop();
        _isPointerInside = false;
        SetActive(false);
    }

    public void Dispose()
    {
        _showTimer.Stop();
        _hideTimer.Stop();
    }

    private void ScheduleShow()
    {
        _showTimer.Stop();

        if (_timing.ShowDelay <= TimeSpan.Zero)
        {
            ShowIfHovered();
            return;
        }

        _showTimer.Interval = _timing.ShowDelay;
        _showTimer.Start();
    }

    private void ScheduleHide()
    {
        _hideTimer.Stop();

        var due = _timing.HideDelay;
        if (_visibleSinceUtc != DateTime.MinValue)
        {
            var elapsed = DateTime.UtcNow - _visibleSinceUtc;
            var remainingVisible = _timing.MinVisibleDuration - elapsed;
            if (remainingVisible > due)
                due = remainingVisible;
        }

        if (due <= TimeSpan.Zero)
        {
            HideIfIdle();
            return;
        }

        _hideTimer.Interval = due;
        _hideTimer.Start();
    }

    private void ShowIfHovered()
    {
        if (!_isPointerInside)
            return;

        SetActive(true);
    }

    private void HideIfIdle()
    {
        if (_isPointerInside)
            return;

        SetActive(false);
    }

    private void SetActive(bool isActive)
    {
        if (_getIsActive() == isActive)
            return;

        _setIsActive(isActive);
        _visibleSinceUtc = isActive ? DateTime.UtcNow : DateTime.MinValue;
    }

    private static DispatcherTimer CreateTimer(Action callback)
    {
        var timer = new DispatcherTimer();
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            callback();
        };

        return timer;
    }
}
