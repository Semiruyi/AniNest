using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LocalPlayer.Models;
using LocalPlayer.Services;

namespace LocalPlayer.Views;

public partial class MainPage : System.Windows.Controls.UserControl
{
    private readonly SettingsService settingsService = new();
    private readonly ObservableCollection<FolderListItem> folderItems = new();

    public event Action<object, string, string>? FolderSelected;

    // 拖拽状态
    private FolderListItem? _dragSourceItem;
    private System.Windows.Point _dragStartPoint;
    private bool _isDragging;
    private DragAdorner? _dragAdorner;

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
        var folders = settingsService.GetFolders();
        folderItems.Clear();
        foreach (var folder in folders)
        {
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
        }
        FolderList.ItemsSource = folderItems;
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

            var border = FindTemplateRoot(item);
            if (border != null)
            {
                PlayDeleteAnimation(border, () =>
                {
                    folderItems.Remove(item);
                    settingsService.RemoveFolder(path);
                });
            }
            else
            {
                folderItems.Remove(item);
                settingsService.RemoveFolder(path);
            }
        }
        e.Handled = true;
    }

    private void PlayDeleteAnimation(UIElement element, Action onComplete)
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

        animOpacity.Completed += (_, _) =>
        {
            element.RenderTransform = Transform.Identity;
            onComplete();
        };

        element.BeginAnimation(OpacityProperty, animOpacity);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleX);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleY);
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

            // 新卡片淡入动画
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var border = FindTemplateRoot(newItem);
                if (border != null)
                {
                    var translate = new TranslateTransform(0, 20);
                    border.RenderTransform = translate;
                    border.Opacity = 0;

                    var animOpacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    var animY = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(300))
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

    // ========== 拖拽排序 ==========

    private void Card_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.Tag is not string path) return;

        _dragSourceItem = folderItems.FirstOrDefault(i => i.Path == path);
        _dragStartPoint = e.GetPosition(FolderList);
        _isDragging = false;
        border.CaptureMouse();
    }

    private void Card_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragSourceItem == null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            CancelDrag();
            return;
        }

        System.Windows.Point currentPos = e.GetPosition(FolderList);
        Vector diff = _dragStartPoint - currentPos;

        if (!_isDragging && (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5))
        {
            _isDragging = true;
            StartDrag(sender as Border);
        }

        if (_isDragging && _dragAdorner != null && sender is Border border)
        {
            System.Windows.Point posInBorder = FolderList.TranslatePoint(currentPos, border);
            _dragAdorner.UpdatePosition(posInBorder);
        }
    }

    private void Card_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border)
        {
            border.ReleaseMouseCapture();
        }

        if (_isDragging && _dragSourceItem != null)
        {
            System.Windows.Point dropPos = e.GetPosition(FolderList);
            int targetIndex = CalculateTargetIndex(dropPos);
            int sourceIndex = folderItems.IndexOf(_dragSourceItem);

            if (targetIndex > sourceIndex) targetIndex--;

            if (targetIndex >= 0 && targetIndex < folderItems.Count && targetIndex != sourceIndex)
            {
                folderItems.Move(sourceIndex, targetIndex);
                var paths = folderItems.Select(i => i.Path).ToList();
                settingsService.ReorderFolders(paths);
            }

            e.Handled = true;
        }

        CancelDrag();
    }

    private void StartDrag(Border? border)
    {
        if (border == null || _dragSourceItem == null) return;

        border.Opacity = 0.4;

        var adornerLayer = AdornerLayer.GetAdornerLayer(FolderList);
        if (adornerLayer != null)
        {
            _dragAdorner = new DragAdorner(border);
            adornerLayer.Add(_dragAdorner);
        }
    }

    private void CancelDrag()
    {
        if (_dragAdorner != null)
        {
            var adornerLayer = AdornerLayer.GetAdornerLayer(FolderList);
            adornerLayer?.Remove(_dragAdorner);
            _dragAdorner = null;
        }

        if (_dragSourceItem != null)
        {
            var border = FindTemplateRoot(_dragSourceItem);
            if (border != null)
            {
                border.BeginAnimation(OpacityProperty, null);
                border.Opacity = 1;
            }
        }

        _dragSourceItem = null;
        _isDragging = false;
    }

    private int CalculateTargetIndex(System.Windows.Point dropPos)
    {
        for (int i = 0; i < folderItems.Count; i++)
        {
            var container = FolderList.ItemContainerGenerator.ContainerFromItem(folderItems[i]);
            if (container is not FrameworkElement fe) continue;

            System.Windows.Point containerPos = fe.TranslatePoint(new System.Windows.Point(0, 0), FolderList);
            System.Windows.Rect rect = MakeRect(containerPos, fe.ActualWidth, fe.ActualHeight);

            if (rect.Contains(dropPos))
            {
                if (dropPos.X < rect.X + rect.Width / 2)
                    return i;
                else
                    return i + 1;
            }
        }
        return folderItems.Count;
    }

    // 显式类型别名，避免 System.Drawing 冲突
    private static System.Windows.Rect MakeRect(System.Windows.Point pos, double w, double h) => new(pos, new System.Windows.Size(w, h));

    // ========== 打开文件夹 ==========

    private void FolderCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging) return;
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

/// <summary>
/// 拖拽时的半透明卡片幽灵副本。
/// </summary>
public record FolderListItem(string Name, string Path, int VideoCount, string? CoverPath);

public class DragAdorner : Adorner
{
    private System.Windows.Point _offset;
    private readonly VisualBrush _brush;

    public DragAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
        _brush = new VisualBrush(adornedElement)
        {
            Opacity = 0.75,
            Stretch = Stretch.None
        };
    }

    public void UpdatePosition(System.Windows.Point position)
    {
        _offset = position;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var rect = new Rect(_offset, AdornedElement.RenderSize);
        drawingContext.DrawRectangle(_brush, null, rect);
    }
}
