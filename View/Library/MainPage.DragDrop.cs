using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LocalPlayer.Primitives;
using LocalPlayer.Model;

namespace LocalPlayer.View.Library;

public partial class MainPage
{
    private void Card_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not string path) return;
        if (IsOriginalSourceInsideButton(e.OriginalSource, border)) return;

        _dragItem = _vm.FolderItems.FirstOrDefault(i => i.Path == path);
        _dragStartPoint = e.GetPosition(null);
        _dragInitiated = false;
    }

    private static bool IsOriginalSourceInsideButton(object? originalSource, Border cardBorder)
    {
        if (originalSource is not DependencyObject dep) return false;
        DependencyObject? current = dep;
        while (current != null && current != cardBorder)
        {
            if (current is System.Windows.Controls.Button) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private void Card_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragItem == null || _dragInitiated) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _dragItem = null;
            return;
        }

        var currentPos = e.GetPosition(null);
        var diff = _dragStartPoint - currentPos;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            _dragInitiated = true;
            var sourceItem = _dragItem;

            var sourceBorder = FindTemplateRoot(sourceItem);
            if (sourceBorder != null) sourceBorder.Opacity = 0.4;

            var oldPositions = CaptureCardPositions();
            var data = new System.Windows.DataObject("FolderListItem", sourceItem);
            System.Windows.DragDrop.DoDragDrop(FolderList, data, System.Windows.DragDropEffects.Move);

            if (sourceBorder != null)
            {
                sourceBorder.BeginAnimation(UIElement.OpacityProperty, null);
                sourceBorder.Opacity = 1;
            }
            _dragItem = null;
            _dragInitiated = false;
            HideInsertionIndicator();
            AnimateCardsReposition(oldPositions);
        }
    }

    private void Card_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragItem != null && !_dragInitiated)
        {
            if (sender is Border border && border.Tag is string path)
            {
                _vm.TrySelectFolder(path, out _);
            }
        }
        _dragItem = null;
        _dragInitiated = false;
    }

    private void FolderList_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("FolderListItem"))
        {
            e.Effects = System.Windows.DragDropEffects.None;
            return;
        }

        e.Effects = System.Windows.DragDropEffects.Move;
        e.Handled = true;

        var pos = e.GetPosition(FolderList);
        int targetIndex = CalculateTargetIndex(pos);
        UpdateInsertionIndicator(targetIndex);
    }

    private void FolderList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("FolderListItem") ||
            e.Data.GetData("FolderListItem") is not FolderListItem sourceItem)
            return;

        var pos = e.GetPosition(FolderList);
        int targetIndex = CalculateTargetIndex(pos);
        int sourceIndex = _vm.FolderItems.IndexOf(sourceItem);

        if (sourceIndex < 0) return;

        if (targetIndex > sourceIndex) targetIndex--;

        if (targetIndex >= 0 && targetIndex < _vm.FolderItems.Count && targetIndex != sourceIndex)
        {
            _vm.FolderItems.Move(sourceIndex, targetIndex);
            var paths = _vm.FolderItems.Select(i => i.Path).ToList();
            _vm.ReorderFolders(paths);
        }

        HideInsertionIndicator();
        e.Handled = true;
    }

    private void FolderList_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        HideInsertionIndicator();
    }

    private int CalculateTargetIndex(System.Windows.Point dropPos)
    {
        for (int i = 0; i < _vm.FolderItems.Count; i++)
        {
            var container = FolderList.ItemContainerGenerator.ContainerFromItem(_vm.FolderItems[i]);
            if (container is not FrameworkElement fe) continue;

            var containerPos = fe.TranslatePoint(new System.Windows.Point(0, 0), FolderList);
            var rect = new System.Windows.Rect(containerPos, new System.Windows.Size(fe.ActualWidth, fe.ActualHeight));

            if (rect.Contains(dropPos))
            {
                return dropPos.X < rect.X + rect.Width / 2 ? i : i + 1;
            }
        }
        return _vm.FolderItems.Count;
    }

    private void UpdateInsertionIndicator(int targetIndex)
    {
        if (_insertionAdorner == null)
        {
            var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(FolderList);
            if (layer == null) return;
            _insertionAdorner = new InsertionAdorner(FolderList);
            layer.Add(_insertionAdorner);
        }

        if (targetIndex < _vm.FolderItems.Count)
        {
            var container = FolderList.ItemContainerGenerator.ContainerFromItem(_vm.FolderItems[targetIndex]);
            if (container is FrameworkElement fe)
            {
                var pos = fe.TranslatePoint(new System.Windows.Point(0, 0), FolderList);
                _insertionAdorner.ShowAt(pos.X - 4, pos.Y, fe.ActualHeight);
            }
        }
        else if (_vm.FolderItems.Count > 0)
        {
            var container = FolderList.ItemContainerGenerator.ContainerFromItem(_vm.FolderItems[^1]);
            if (container is FrameworkElement fe)
            {
                var pos = fe.TranslatePoint(new System.Windows.Point(0, 0), FolderList);
                _insertionAdorner.ShowAt(pos.X + fe.ActualWidth + 8, pos.Y, fe.ActualHeight);
            }
        }
    }

    private void HideInsertionIndicator()
    {
        if (_insertionAdorner == null) return;
        _insertionAdorner.Hide();
        _insertionAdorner = null;
    }
}
