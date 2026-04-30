using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LocalPlayer.View.Primitives;
using LocalPlayer.View.Animations;
using LocalPlayer.Model;
using LocalPlayer.ViewModel;

namespace LocalPlayer.View.Library;

public partial class MainPage : System.Windows.Controls.UserControl
{
    private readonly MainPageViewModel _vm;

    public event Action<object, string, string>? FolderSelected;

    private FolderListItem? _dragItem;
    private System.Windows.Point _dragStartPoint;
    private bool _dragInitiated;
    private InsertionAdorner? _insertionAdorner;

    public MainPage(MainPageViewModel vm)
    {
        _vm = vm;
        DataContext = _vm;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        MainPageViewModel.Log("MainPage 构造函数开始");
        InitializeComponent();
        App.LogStartup($"MainPage.InitializeComponent 完成，耗时 {sw.ElapsedMilliseconds}ms");

        _vm.FolderSelected += (s, path, name) => FolderSelected?.Invoke(s, path, name);

        Loaded += MainPage_Loaded;
        App.LogStartup($"MainPage 构造函数完成，总耗时 {sw.ElapsedMilliseconds}ms");
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        MainPageViewModel.Log("MainPage.Loaded 事件触发");

        FolderList.ItemsSource = _vm.FolderItems;
        App.LogStartup($"MainPage UI 初始显示完成，耗时 {sw.ElapsedMilliseconds}ms");

        var loadedItems = await Task.Run(() => _vm.LoadFoldersData());
        App.LogStartup($"MainPage 后台数据加载完成，耗时 {sw.ElapsedMilliseconds}ms");

        FolderList.Opacity = 0;
        _vm.FolderItems.Clear();
        foreach (var item in loadedItems)
            _vm.FolderItems.Add(item);
        _ = Dispatcher.BeginInvoke(new Action(AnimateCardsEntrance), DispatcherPriority.Loaded);

        _vm.EnqueueAllFolders(loadedItems);

        App.LogStartup($"MainPage.Loaded 总耗时 {sw.ElapsedMilliseconds}ms");
    }

    // ========== 删除 ==========

    private void DeleteBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string path)
        {
            var item = _vm.FolderItems.FirstOrDefault(i => i.Path == path);
            if (item == null) return;

            var container = FolderList.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
            if (container != null)
            {
                PlayDeleteAnimation(container, () => _vm.DeleteFolder(item));
            }
            else
            {
                _vm.DeleteFolder(item);
            }
        }
        e.Handled = true;
    }

    private void PlayDeleteAnimation(FrameworkElement element, Action onComplete)
    {
        var scale = new ScaleTransform(1, 1);
        element.RenderTransform = scale;
        element.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

        bool finished = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (finished) return;
            finished = true;
            element.RenderTransform = Transform.Identity;
            onComplete();
        };
        timer.Start();

        AnimationHelper.AnimateFromCurrent(element, UIElement.OpacityProperty, 0, 250, AnimationHelper.EaseOut, () =>
        {
            if (finished) return;
            finished = true;
            timer.Stop();
            element.RenderTransform = Transform.Identity;
            onComplete();
        });
        AnimationHelper.AnimateScaleTransform(scale, 0.85, 250, AnimationHelper.EaseOut);
    }

    // ========== 视觉辅助 ==========

    private Border? FindTemplateRoot(FolderListItem item)
    {
        var container = FolderList.ItemContainerGenerator.ContainerFromItem(item);
        return FindChildBorder(container);
    }

    private static Border? FindChildBorder(DependencyObject? parent)
    {
        if (parent == null) return null;
        if (parent is Border b) return b;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            var result = FindChildBorder(child);
            if (result != null) return result;
        }
        return null;
    }
}
