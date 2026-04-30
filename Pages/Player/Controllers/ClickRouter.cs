using System;
using System.Windows.Input;
using System.Windows.Threading;

namespace LocalPlayer.Pages.Player;

/// <summary>
/// 单击/双击路由器：在等待窗口内区分单击和双击。
/// PlayerPage 和 FullscreenWindow 共用。
/// </summary>
public class ClickRouter
{
    private readonly Action _onSingleClick;
    private readonly Action _onDoubleClick;
    private readonly DispatcherTimer _timer;

    public ClickRouter(Action onSingleClick, Action onDoubleClick, int intervalMs = 400)
    {
        _onSingleClick = onSingleClick;
        _onDoubleClick = onDoubleClick;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            _onSingleClick();
        };
    }

    public void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            _timer.Stop();
            _onDoubleClick();
            e.Handled = true;
            return;
        }

        _timer.Stop();
        _timer.Start();
    }
}
