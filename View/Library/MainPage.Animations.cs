using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LocalPlayer.View.Animations;
using LocalPlayer.Model;

namespace LocalPlayer.View.Library;

public partial class MainPage
{
    private void AnimateCardsEntrance()
    {
        if (!IsLoaded) return;

        var borders = new List<FrameworkElement>();
        foreach (var item in _vm.FolderItems)
        {
            var container = FolderList.ItemContainerGenerator.ContainerFromItem(item);
            var border = FindChildBorder(container);
            if (border != null)
                borders.Add(border);
        }

        _ = StaggeredEntranceAnimator.AnimateAsync(borders);
        FolderList.Opacity = 1;
    }

    internal Dictionary<FolderListItem, System.Windows.Point> CaptureCardPositions()
    {
        var positions = new Dictionary<FolderListItem, System.Windows.Point>();
        foreach (var item in _vm.FolderItems)
        {
            var container = FolderList.ItemContainerGenerator.ContainerFromItem(item);
            var border = FindChildBorder(container);
            if (border != null)
            {
                var pos = border.TranslatePoint(new System.Windows.Point(0, 0), FolderList);
                positions[item] = pos;
            }
        }
        return positions;
    }

    internal void AnimateCardsReposition(Dictionary<FolderListItem, System.Windows.Point> oldPositions)
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

                var newPos = border.TranslatePoint(new System.Windows.Point(0, 0), FolderList);
                var deltaX = oldPos.X - newPos.X;
                var deltaY = oldPos.Y - newPos.Y;

                if (Math.Abs(deltaX) < 0.5 && Math.Abs(deltaY) < 0.5)
                    continue;

                var originalTransform = border.RenderTransform;
                var translate = new TranslateTransform(deltaX, deltaY);
                border.RenderTransform = translate;

                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var dur = TimeSpan.FromMilliseconds(350);
                var delay = TimeSpan.FromMilliseconds(i * 25);

                var animX = new DoubleAnimation(deltaX, 0, dur)
                {
                    BeginTime = delay,
                    EasingFunction = ease
                };
                var animY = new DoubleAnimation(deltaY, 0, dur)
                {
                    BeginTime = delay,
                    EasingFunction = ease
                };

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

    internal void AnimateCardAdded(FolderListItem newItem)
    {
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            var container = FolderList.ItemContainerGenerator.ContainerFromItem(newItem);
            var border = FindChildBorder(container);
            if (border == null) return;

            _ = StaggeredEntranceAnimator.AnimateSingleAsync(border);
        }), DispatcherPriority.Loaded);
    }
}
