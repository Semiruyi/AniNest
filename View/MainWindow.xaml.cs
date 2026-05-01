using System.Windows;
using LocalPlayer.ViewModel;

namespace LocalPlayer.View;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}
