using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LocalPlayer.Models;
using LocalPlayer.Services;

namespace LocalPlayer.Views;

public partial class MainPage : System.Windows.Controls.UserControl
{
    private readonly SettingsService settingsService = new();
    private readonly ObservableCollection<FolderListItem> folderItems = new();

    public event Action<object, string, string>? FolderSelected;

    private FolderListItem? _dragItem;
    private System.Windows.Point _dragStartPoint;
    private bool _dragInitiated;
    private InsertionAdorner? _insertionAdorner;

    public MainPage()
    {
        InitializeComponent();
        Loaded += MainPage_Loaded;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadFolders();
    }

    private void LoadFolders()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        System.Diagnostics.Debug.WriteLine($"[MainPage] LoadFolders 开始");
        var folders = settingsService.GetFolders();
        System.Diagnostics.Debug.WriteLine($"[MainPage] GetFolders 耗时 {sw.ElapsedMilliseconds}ms，共 {folders.Count} 个文件夹");
        folderItems.Clear();
        foreach (var folder in folders)
        {
            var folderSw = System.Diagnostics.Stopwatch.StartNew();
            if (Directory.Exists(folder.Path))
            {
                int count = VideoScanner.CountVideosInFolder(folder.Path);
                string? coverPath = GetCoverPath(folder.Path);
                folderItems.Add(new FolderListItem(folder.Name, folder.Path, count, coverPath));
            }
            else
            {
                settingsService.RemoveFolder(folder.Path);
            }
            System.Diagnostics.Debug.WriteLine($"[MainPage] 文件夹 {folder.Name} 处理耗时 {folderSw.ElapsedMilliseconds}ms");
        }
        FolderList.ItemsSource = folderItems;
        UpdateToolbarState();
        System.Diagnostics.Debug.WriteLine($"[MainPage] LoadFolders 总耗时 {sw.ElapsedMilliseconds}ms");
    }

    private void UpdateToolbarState()
    {
        int count = folderItems.Count;
        FolderCountText.Text = $"{count} 个文件夹";
        EmptyHintText.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string? GetCoverPath(string folderPath)
    {
        string specificCover = Path.Combine(folderPath, "cover.jpg");
        if (File.Exists(specificCover))
        {
            return specificCover;
        }
        return VideoScanner.FindCoverImage(folderPath);
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
                    folderItems.Remove(item);
                    settingsService.RemoveFolder(path);
                    UpdateToolbarState();
                });
            }
            else
            {
                folderItems.Remove(item);
                settingsService.RemoveFolder(path);
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

        var animOpacity = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var animScaleX = new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var animScaleY = new DoubleAnimation(1, 0.85, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

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

        animOpacity.Completed += (_, _) =>
        {
            if (finished) return;
            finished = true;
            timer.Stop();
            element.RenderTransform = Transform.Identity;
            onComplete();
        };

        element.BeginAnimation(OpacityProperty, animOpacity);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleX);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleY);
    }

    // ========== 设置 ==========

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("设置功能开发中...\n\n未来将支持：\n- 设置默认扫描文件夹", "提示");
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

            int count = VideoScanner.CountVideosInFolder(path);
            if (count == 0)
            {
                System.Windows.MessageBox.Show("该文件夹内没有视频文件", "提示");
                return;
            }

            settingsService.AddFolder(path, name);
            var newItem = new FolderListItem(name, path, count, GetCoverPath(path));
            folderItems.Add(newItem);
            UpdateToolbarState();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var border = FindTemplateRoot(newItem);
                if (border != null)
                {
                    var translate = new TranslateTransform(0, 20);
                    border.RenderTransform = translate;
                    border.Opacity = 0;

                    var animOpacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    var animY = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(500))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    animY.Completed += (_, _) => border.RenderTransform = Transform.Identity;

                    border.BeginAnimation(OpacityProperty, animOpacity);
                    translate.BeginAnimation(TranslateTransform.YProperty, animY);
                }
            }), System.Windows.Threading.DispatcherPriority.Render);
        }
    }

    // ========== 打开文件夹 ==========

    private void FolderCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string path)
        {
            string name = Path.GetFileName(path);
            var videos = VideoScanner.GetVideoFiles(path);
            if (videos.Length == 0)
            {
                System.Windows.MessageBox.Show("文件夹内没有视频文件", "提示");
                return;
            }
            FolderSelected?.Invoke(this, path, name);
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
}
