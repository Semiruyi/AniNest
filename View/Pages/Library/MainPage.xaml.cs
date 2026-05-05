using System.Windows;
using System.Windows.Media;
using LocalPlayer.View.Diagnostics;
using LocalPlayer.ViewModel;

namespace LocalPlayer.View.Pages.Library;

public partial class MainPage : System.Windows.Controls.UserControl
{
    private PerfSceneSession? _initialLoadScene;
    private MainPageViewModel? _viewModel;
    private bool _initialLoadCompleted;
    private int _renderFramesAfterLoadCompleted;

    public MainPage(MainPageViewModel vm)
    {
        DataContext = vm;
        _viewModel = vm;
        // 必须在 Loaded 之前订阅，因为 LoadedCommandBehavior 触发
        // LoadDataCommand 时 LoadDataCompleted 可能立即同步发出
        _viewModel.LoadDataCompleted += OnLoadDataCompleted;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialLoadScene != null)
            return;

        _initialLoadCompleted = false;
        _renderFramesAfterLoadCompleted = 0;
        _initialLoadScene = PerfScenes.Begin("Library.InitialLoad");

        CompositionTarget.Rendering += OnRendering;

        // 直接触发加载，不依赖 LoadedCommandBehavior（事件时序问题）
        if (_viewModel != null)
            await _viewModel.LoadDataCommand.ExecuteAsync(null);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        CompleteInitialLoadScene();
    }

    private void OnLoadDataCompleted(object? sender, EventArgs e)
    {
        _initialLoadCompleted = true;
        _renderFramesAfterLoadCompleted = 0;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_initialLoadCompleted || _initialLoadScene == null)
            return;

        _renderFramesAfterLoadCompleted++;
        if (_renderFramesAfterLoadCompleted >= 2)
            CompleteInitialLoadScene();
    }

    private void CompleteInitialLoadScene()
    {
        if (_initialLoadScene == null)
            return;

        _initialLoadScene.Stop();
        _initialLoadScene = null;
        _initialLoadCompleted = false;
        _renderFramesAfterLoadCompleted = 0;
    }
}
