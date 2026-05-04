using LocalPlayer.View.Primitives;
using LocalPlayer.ViewModel.Player;

namespace LocalPlayer.View.Pages.Player;

public partial class PlayerPage : System.Windows.Controls.UserControl
{
    public PlayerPage(PlayerViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        var coordinator = PopupInputCoordinator.Instance;
        coordinator.RegisterRegion(VideoContainer, PopupHitKind.VideoSurface);
        coordinator.RegisterRegion(PlaylistPanel, PopupHitKind.ControlBarInteractive);
        coordinator.RegisterRegion(PageRoot, PopupHitKind.DismissBackground);
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        var coordinator = PopupInputCoordinator.Instance;
        coordinator.UnregisterRegion(VideoContainer, PopupHitKind.VideoSurface);
        coordinator.UnregisterRegion(PlaylistPanel, PopupHitKind.ControlBarInteractive);
        coordinator.UnregisterRegion(PageRoot, PopupHitKind.DismissBackground);
    }
}
