using System;
using System.Windows;
using System.Windows.Controls;
using LocalPlayer.ViewModel;

namespace LocalPlayer.View.Player;

public partial class PlayerPage : UserControl
{
    private readonly PlayerViewModel _vm;
    private bool _disposed;

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

        _vm.InitializeMedia();
        VideoImage.Source = _vm.VideoSource;

        Unloaded += (_, _) => Cleanup();
    }

    public void LoadFolder(string folderPath, string folderName)
        => _vm.LoadFolder(folderPath, folderName);

    public void Cleanup()
    {
        if (_disposed) return;
        _disposed = true;
        _vm.SaveProgress();
        _vm.DisposeMedia();
    }
}
