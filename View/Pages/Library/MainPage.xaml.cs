using LocalPlayer.ViewModel;

namespace LocalPlayer.View.Pages.Library;

public partial class MainPage : System.Windows.Controls.UserControl
{
    public MainPage(MainPageViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}
