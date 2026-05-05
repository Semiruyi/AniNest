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
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialLoadScene != null)
            return;

        _initialLoadCompleted = false;
        _renderFramesAfterLoadCompleted = 0;
        _initialLoadScene = PerfScenes.Begin("Library.InitialLoad");
        _viewModel = DataContext as MainPageViewModel;
        if (_viewModel != null)
            _viewModel.LoadDataCompleted += OnLoadDataCompleted;

        CompositionTarget.Rendering += OnRendering;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
            _viewModel.LoadDataCompleted -= OnLoadDataCompleted;

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
