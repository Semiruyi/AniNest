using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LocalPlayer.View.Animations;
using LocalPlayer.Model;

namespace LocalPlayer.View.Library;

public partial class MainPage
{
    private IEnumerable<Border> EnumerateCardBorders()
    {
        foreach (var item in _vm.FolderItems)
        {
            var container = FolderList.ItemContainerGenerator.ContainerFromItem(item);
            var border = FindChildBorder(container);
            if (border != null)
                yield return border;
        }
    }

    private void AnimateCardsEntrance()
    {
        if (!IsLoaded) return;

        var borders = new List<FrameworkElement>();
        foreach (var border in EnumerateCardBorders())
            borders.Add(border);

        _ = StaggeredEntranceAnimator.AnimateAsync(borders);
        FolderList.Opacity = 1;
    }

    internal Dictionary<FolderListItem, Point> CaptureCardPositions()
    {
        var positions = new Dictionary<FolderListItem, Point>();
        foreach (var item in _vm.FolderItems)
        {
            var container = FolderList.ItemContainerGenerator.ContainerFromItem(item);
            var border = FindChildBorder(container);
            if (border != null)
                positions[item] = border.TranslatePoint(new Point(0, 0), FolderList);
        }
        return positions;
    }

    internal void AnimateCardsReposition(Dictionary<FolderListItem, Point> oldPositions)
    {
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            for (int i = 0; i < _vm.FolderItems.Count; i++)
            {
                var item = _vm.FolderItems[i];
                if (!oldPositions.TryGetValue(item, out var oldPos))
                    continue;

                var container = FolderList.ItemContainerGenerator.ContainerFromItem(item);
                var border = FindChildBorder(container);
                if (border == null) continue;

                var newPos = border.TranslatePoint(new Point(0, 0), FolderList);
                var deltaX = oldPos.X - newPos.X;
                var deltaY = oldPos.Y - newPos.Y;

                if (Math.Abs(deltaX) < 0.5 && Math.Abs(deltaY) < 0.5)
                    continue;

                var originalTransform = border.RenderTransform;
                var translate = new TranslateTransform(deltaX, deltaY);
                border.RenderTransform = translate;

                var animX = AnimationHelper.CreateAnim(deltaX, 0, 350, AnimationHelper.EaseOut, beginTimeMs: i * 25);
                var animY = AnimationHelper.CreateAnim(deltaY, 0, 350, AnimationHelper.EaseOut, beginTimeMs: i * 25);

                EventHandler? cleanupHandler = null;
                cleanupHandler = (_, _) =>
                {
                    animX.Completed -= cleanupHandler;
                    translate.BeginAnimation(TranslateTransform.XProperty, null);
                    translate.BeginAnimation(TranslateTransform.YProperty, null);
                    if (border.RenderTransform == translate)
                        border.RenderTransform = originalTransform;
                };
                animX.Completed += cleanupHandler;

                translate.BeginAnimation(TranslateTransform.XProperty, animX);
                translate.BeginAnimation(TranslateTransform.YProperty, animY);
            }
        }), DispatcherPriority.Loaded);
    }
}
