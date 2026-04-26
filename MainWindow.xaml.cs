using System.Windows;
using System.Windows.Input;
using LocalPlayer.Views;

namespace LocalPlayer;

public partial class MainWindow : Window
{
    private MainPage? mainPage;
    private PlayerPage? playerPage;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        KeyDown += MainWindow_KeyDown;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ShowMainPage();
    }

    private void ShowMainPage()
    {
        mainPage = new MainPage();
        mainPage.FolderSelected += MainPage_FolderSelected;
        PageHost.Content = mainPage;
        playerPage = null;
    }

    private void MainPage_FolderSelected(object? sender, string folderPath, string folderName)
    {
        playerPage = new PlayerPage();
        playerPage.BackRequested += PlayerPage_BackRequested;
        playerPage.LoadFolder(folderPath, folderName);
        PageHost.Content = playerPage;
    }

    private void PlayerPage_BackRequested(object? sender, System.EventArgs e)
    {
        playerPage?.Dispose();
        ShowMainPage();
    }

    private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (playerPage != null && PageHost.Content == playerPage)
        {
            playerPage.HandleKeyDown(e);
        }
    }
}
