using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LocalPlayer.Models;

namespace LocalPlayer.Views;

public partial class MainPage
{
    private void AnimateCardsEntrance()
    {
        if (!IsLoaded) return;

        var borders = new List<Border>();
        foreach (var item in folderItems)
        {
            var container = FolderList.ItemContainerGenerator.ContainerFromItem(item);
            var border = FindChildBorder(container);
            if (border != null)
                borders.Add(border);
        }

        for (int i = 0; i < borders.Count; i++)
        {
            var border = borders[i];
            var delay = TimeSpan.FromMilliseconds(i * 35);

            border.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            var st = new ScaleTransform(0, 0);
            border.RenderTransform = st;
            border.Opacity = 0;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(420);

            var scaleAnimX = new DoubleAnimation(0, 1.0, dur)
            {
                BeginTime = delay,
                EasingFunction = ease
            };
            var scaleAnimY = new DoubleAnimation(0, 1.0, dur)
            {
                BeginTime = delay,
                EasingFunction = ease
            };
            var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320))
            {
                BeginTime = delay
            };

            st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimX);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimY);
            border.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }

        FolderList.Opacity = 1;
    }

    internal Dictionary<FolderListItem, System.Windows.Point> CaptureCardPositions()
    {
        var positions = new Dictionary<FolderListItem, System.Windows.Point>();
        foreach (var item in folderItems)
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
            for (int i = 0; i < folderItems.Count; i++)
            {
                var item = folderItems[i];
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

            border.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            border.RenderTransform = new ScaleTransform(0, 0);
            border.Opacity = 0;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(380);

            var st = (ScaleTransform)border.RenderTransform;
            var scaleAnimX = new DoubleAnimation(0, 1.0, dur) { EasingFunction = ease };
            var scaleAnimY = new DoubleAnimation(0, 1.0, dur) { EasingFunction = ease };
            var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));

            st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimX);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimY);
            border.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }), DispatcherPriority.Loaded);
    }
}
