using System.Windows;
using System.Windows.Controls;
using AniNest.Presentation.Behaviors;

namespace AniNest.View;

public partial class MainWindowTitleBar : UserControl
{
    private static readonly HoverPopupTiming BackgroundTaskPopupTiming =
        new(TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(120));

    public static readonly DependencyProperty IsBackgroundTaskPopupOpenProperty =
        DependencyProperty.Register(
            nameof(IsBackgroundTaskPopupOpen),
            typeof(bool),
            typeof(MainWindowTitleBar),
            new PropertyMetadata(false));

    private readonly HoverPopupController _backgroundTaskPopupController;

    public MainWindowTitleBar()
    {
        _backgroundTaskPopupController = new HoverPopupController(
            BackgroundTaskPopupTiming,
            () => IsBackgroundTaskPopupOpen,
            opened => IsBackgroundTaskPopupOpen = opened);
        InitializeComponent();
        Unloaded += (_, _) => _backgroundTaskPopupController.Dispose();
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
    public HoverPopupController BackgroundTaskPopupController => _backgroundTaskPopupController;
}
