using LocalPlayer.ViewModel;

namespace LocalPlayer.View.Library;

public partial class MainPage : System.Windows.Controls.UserControl
{
    public MainPage(MainPageViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}
