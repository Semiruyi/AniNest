using System.Windows;
using System.Windows.Input;
using LocalPlayer.View.Diagnostics;
using LocalPlayer.ViewModel;

namespace LocalPlayer.View;

public partial class MainWindow : Window
{
    private readonly FpsMonitor _fps;

    public MainWindow(ShellViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        _fps = new FpsMonitor(this);
        _fps.Attach();
    }
}
