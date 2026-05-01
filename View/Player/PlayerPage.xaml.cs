using System;
using System.Windows;
using System.Windows.Controls;
using LocalPlayer.ViewModel;

namespace LocalPlayer.View.Player;

public partial class PlayerPage : UserControl, IDisposable
{
    private readonly PlayerViewModel _vm;

    public event EventHandler? BackRequested;

    public PlayerPage(PlayerViewModel vm)
    {
        _vm = vm;
        DataContext = _vm;

        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            PlayerViewModel.LogError("构造函数异常", ex);
            throw;
        }

        Loaded += PlayerPage_Loaded;
        Unloaded += PlayerPage_Unloaded;

        _vm.BackRequested += () =>
        {
            _vm.SaveProgress();
            BackRequested?.Invoke(this, EventArgs.Empty);
        };
        _vm.OpenKeyBindingsRequested += () =>
        {
            var window = new View.Settings.KeyBindingsWindow(new KeyBindingsViewModel(_vm.InputHandler))
            {
                Owner = Window.GetWindow(this)
            };
            window.ShowDialog();
        };
    }

    private void PlayerPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _vm.InitializeMedia();
            VideoImage.Source = _vm.VideoSource;

            if (_pendingFolderPath != null)
            {
                var path = _pendingFolderPath;
                var name = _pendingFolderName ?? "";
                _pendingFolderPath = null;
                _pendingFolderName = null;
                LoadFolder(path, name);
            }
        }
        catch (Exception ex)
        {
            PlayerViewModel.LogError("Loaded 异常", ex);
            throw;
        }
    }

    private string? _pendingFolderPath;
    private string? _pendingFolderName;

    private void PlayerPage_Unloaded(object sender, RoutedEventArgs e)
    {
        Dispose();
    }

    public void LoadFolder(string folderPath, string folderName)
    {
        if (!IsLoaded)
        {
            _pendingFolderPath = folderPath;
            _pendingFolderName = folderName;
            return;
        }
        _vm.LoadFolder(folderPath, folderName);
    }

    public void Dispose()
    {
        _vm.SaveProgress();
        _vm.DisposeMedia();
    }
}
