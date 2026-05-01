using LocalPlayer.ViewModel.Player;

namespace LocalPlayer.View.Pages.Player;

public partial class PlayerPage : System.Windows.Controls.UserControl
{
    public PlayerPage(PlayerViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}
