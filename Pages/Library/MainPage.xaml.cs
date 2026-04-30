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
using LocalPlayer;
using LocalPlayer.UI.Primitives;
using LocalPlayer.Domain;
using LocalPlayer.Infrastructure;

namespace LocalPlayer.Pages.Library;

public partial class MainPage : System.Windows.Controls.UserControl
{
    private readonly SettingsService settingsService = SettingsService.Instance;
    private readonly ThumbnailGenerator thumbnailGenerator = ThumbnailGenerator.Instance;
    private readonly ObservableCollection<FolderListItem> folderItems = new();

    public event Action<object, string, string>? FolderSelected;

    private FolderListItem? _dragItem;
    private System.Windows.Point _dragStartPoint;
    private bool _dragInitiated;
    private InsertionAdorner? _insertionAdorner;

    public MainPage()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        App.LogStartup("MainPage 构造函数开始");
        InitializeComponent();
        App.LogStartup($"MainPage.InitializeComponent 完成，耗时 {sw.ElapsedMilliseconds}ms");

        thumbnailGenerator.ProgressChanged += (_, args) =>
            Dispatcher.Invoke(() => UpdateThumbnailProgress(args.Ready, args.Total));

        Loaded += MainPage_Loaded;
        App.LogStartup($"MainPage 构造函数完成，总耗时 {sw.ElapsedMilliseconds}ms");
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        App.LogStartup("MainPage.Loaded 事件触发");

        // 立即显示空状态 UI，不等待数据
        FolderList.ItemsSource = folderItems;
        UpdateToolbarState();
        App.LogStartup($"MainPage UI 初始显示完成，耗时 {sw.ElapsedMilliseconds}ms");

        // 后台线程加载配置 + 扫描文件夹
        var loadedItems = await Task.Run(() => LoadFoldersData());
        App.LogStartup($"MainPage 后台数据加载完成，耗时 {sw.ElapsedMilliseconds}ms");

        // 切回 UI 线程刷新卡片（先隐藏防止闪烁，动画开始后再显示）
        FolderList.Opacity = 0;
        folderItems.Clear();
        foreach (var item in loadedItems)
            folderItems.Add(item);
        UpdateToolbarState();
        _ = Dispatcher.BeginInvoke(new Action(AnimateCardsEntrance), DispatcherPriority.Loaded);

        // 入队所有文件夹的缩略图生成
        EnqueueAllFolders(loadedItems);

        App.LogStartup($"MainPage.Loaded 总耗时 {sw.ElapsedMilliseconds}ms (UI 阻塞仅初始化部分)");
    }

    private List<FolderListItem> LoadFoldersData()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var items = new List<FolderListItem>();
        var folders = settingsService.GetFolders();
        App.LogStartup($"  后台 GetFolders 完成，耗时 {sw.ElapsedMilliseconds}ms，共 {folders.Count} 个文件夹");

        foreach (var folder in folders)
        {
            var folderSw = System.Diagnostics.Stopwatch.StartNew();
            if (Directory.Exists(folder.Path))
            {
                var (count, coverPath) = VideoScanner.ScanFolder(folder.Path);
                App.LogStartup($"  ScanFolder({folder.Name}) 完成，耗时 {folderSw.ElapsedMilliseconds}ms，视频 {count} 个");
                items.Add(new FolderListItem(folder.Name, folder.Path, count, coverPath));
            }
            else
            {
                settingsService.RemoveFolder(folder.Path);
                thumbnailGenerator.DeleteForFolder(folder.Path);
                App.LogStartup($"  文件夹 {folder.Name} 路径不存在，已移除");
            }
        }
        App.LogStartup($"  后台 LoadFoldersData 总耗时 {sw.ElapsedMilliseconds}ms");
        return items;
    }

    private void UpdateToolbarState()
    {
        int count = folderItems.Count;
        FolderCountText.Text = $"{count} 个文件夹";
        EmptyHintText.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ========== 删除 ==========

    private void DeleteBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string path)
        {
            var item = folderItems.FirstOrDefault(i => i.Path == path);
            if (item == null) return;

            var container = FolderList.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
            if (container != null)
            {
                PlayDeleteAnimation(container, () =>
                {
                    var oldPositions = CaptureCardPositions();
                    folderItems.Remove(item);
                    settingsService.RemoveFolder(path);
                    thumbnailGenerator.DeleteForFolder(path);
                    UpdateToolbarState();
                    AnimateCardsReposition(oldPositions);
                });
            }
            else
            {
                folderItems.Remove(item);
                settingsService.RemoveFolder(path);
                thumbnailGenerator.DeleteForFolder(path);
                UpdateToolbarState();
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

    // ========== 设置 ==========

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        int currentDays = settingsService.GetThumbnailExpiryDays();
        string input = Microsoft.VisualBasic.Interaction.InputBox(
            $"缩略图过期天数（0=永不过期）：",
            "缩略图设置",
            currentDays.ToString());

        if (int.TryParse(input, out int days) && days >= 0 && days <= 365)
        {
            settingsService.SetThumbnailExpiryDays(days);
            if (days == 0)
                System.Windows.MessageBox.Show("已设置：缩略图永不过期", "提示");
            else
                System.Windows.MessageBox.Show($"已设置：缩略图 {days} 天后过期", "提示");
        }
    }

    // ========== 添加 ==========

    private void AddFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择包含视频的文件夹"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            string path = dialog.SelectedPath;
            string name = Path.GetFileName(path);

            if (folderItems.Any(i => i.Path == path))
            {
                System.Windows.MessageBox.Show("该文件夹已添加", "提示");
                return;
            }

            var (count, coverPath) = VideoScanner.ScanFolder(path);
            if (count == 0)
            {
                System.Windows.MessageBox.Show("该文件夹内没有视频文件", "提示");
                return;
            }

            settingsService.AddFolder(path, name);
            var newItem = new FolderListItem(name, path, count, coverPath);
            folderItems.Add(newItem);
            UpdateToolbarState();
            AnimateCardAdded(newItem);

            // 入队新文件夹的缩略图生成
            EnqueueFolderForThumbnails(newItem);
        }
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

    // ========== 缩略图生成 ==========

    private void EnqueueAllFolders(List<FolderListItem> items)
    {
        foreach (var item in items)
            EnqueueFolderForThumbnails(item);
    }

    private void EnqueueFolderForThumbnails(FolderListItem item)
    {
        int cardOrder = 0;
        var folders = settingsService.GetFolders();
        var folderInfo = folders.FirstOrDefault(f => f.Path == item.Path);
        if (folderInfo != null)
            cardOrder = folderInfo.OrderIndex;

        var folderProgress = settingsService.GetFolderProgress(item.Path);
        string? lastPlayed = folderProgress?.LastVideoPath;

        var playedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var videoFiles = VideoScanner.GetVideoFiles(item.Path);
        foreach (var vf in videoFiles)
        {
            if (settingsService.IsVideoPlayed(vf))
                playedPaths.Add(vf);
        }

        thumbnailGenerator.EnqueueFolder(item.Path, cardOrder, lastPlayed, playedPaths);
    }

    private void UpdateThumbnailProgress(int ready, int total)
    {
        if (total > 0 && ready < total)
        {
            ThumbnailProgressText.Text = $"缩略图 {ready}/{total}";
            ThumbnailProgressText.Visibility = Visibility.Visible;
        }
        else
        {
            ThumbnailProgressText.Visibility = Visibility.Collapsed;
        }
    }

}
