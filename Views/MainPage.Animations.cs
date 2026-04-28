using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

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
}
