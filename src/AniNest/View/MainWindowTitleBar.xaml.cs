using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace AniNest.View;

public partial class MainWindowTitleBar : UserControl
{
    private static readonly TimeSpan BackgroundTaskPopupOpenDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan BackgroundTaskPopupCloseDelay = TimeSpan.FromMilliseconds(120);

    public static readonly DependencyProperty IsBackgroundTaskPopupOpenProperty =
        DependencyProperty.Register(
            nameof(IsBackgroundTaskPopupOpen),
            typeof(bool),
            typeof(MainWindowTitleBar),
            new PropertyMetadata(false));

    private readonly DispatcherTimer _backgroundTaskPopupOpenTimer;
    private readonly DispatcherTimer _backgroundTaskPopupCloseTimer;
    private bool _isHoveringBackgroundTaskHost;
    private bool _isHoveringBackgroundTaskPopup;

    public MainWindowTitleBar()
    {
        _backgroundTaskPopupOpenTimer = CreateTimer(BackgroundTaskPopupOpenDelay, OpenBackgroundTaskPopupIfHovered);
        _backgroundTaskPopupCloseTimer = CreateTimer(BackgroundTaskPopupCloseDelay, CloseBackgroundTaskPopupIfIdle);
        InitializeComponent();
    }

    public bool IsBackgroundTaskPopupOpen
    {
        get => (bool)GetValue(IsBackgroundTaskPopupOpenProperty);
        set => SetValue(IsBackgroundTaskPopupOpenProperty, value);
    }

    public Border TitleBarRootElement => TitleBarRoot;
    public Button FileButtonElement => FileButton;
    public Button BackButtonElement => BackButton;
    public Button SettingsButtonElement => SettingsButton;
    public Grid TitleBarDragZoneElement => TitleBarDragZone;
    public Grid TitleBarFileNameHostElement => TitleBarFileNameHost;
    public Button MinimizeButtonElement => MinimizeButton;
    public Button MaximizeButtonElement => MaximizeButton;
    public Button CloseButtonElement => CloseButton;

    private void BackgroundTaskHost_MouseEnter(object sender, MouseEventArgs e)
    {
        _isHoveringBackgroundTaskHost = true;
        _backgroundTaskPopupCloseTimer.Stop();

        if (IsBackgroundTaskPopupOpen)
            return;

        RestartTimer(_backgroundTaskPopupOpenTimer);
    }

    private void BackgroundTaskHost_MouseLeave(object sender, MouseEventArgs e)
    {
        _isHoveringBackgroundTaskHost = false;
        _backgroundTaskPopupOpenTimer.Stop();

        if (_isHoveringBackgroundTaskPopup)
            return;

        RestartTimer(_backgroundTaskPopupCloseTimer);
    }

    private void BackgroundTaskPopupContent_MouseEnter(object sender, MouseEventArgs e)
    {
        _isHoveringBackgroundTaskPopup = true;
        _backgroundTaskPopupCloseTimer.Stop();
    }

    private void BackgroundTaskPopupContent_MouseLeave(object sender, MouseEventArgs e)
    {
        _isHoveringBackgroundTaskPopup = false;

        if (_isHoveringBackgroundTaskHost)
            return;

        RestartTimer(_backgroundTaskPopupCloseTimer);
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

    private void OpenBackgroundTaskPopupIfHovered()
    {
        if (_isHoveringBackgroundTaskHost || _isHoveringBackgroundTaskPopup)
            IsBackgroundTaskPopupOpen = true;
    }

    private void CloseBackgroundTaskPopupIfIdle()
    {
        if (!_isHoveringBackgroundTaskHost && !_isHoveringBackgroundTaskPopup)
            IsBackgroundTaskPopupOpen = false;
    }
}
