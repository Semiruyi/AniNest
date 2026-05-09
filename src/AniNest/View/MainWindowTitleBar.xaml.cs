using System.Windows.Controls;

namespace AniNest.View;

public partial class MainWindowTitleBar : UserControl
{
    public MainWindowTitleBar()
    {
        InitializeComponent();
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
}
