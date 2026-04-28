using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LocalPlayer.Views;

public partial class PlayerPage
{
    public async Task FadeOutUIAsync(int durationMs = 250)
    {
        var duration = TimeSpan.FromMilliseconds(durationMs);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        var tcs = new TaskCompletionSource<bool>();
        var anim = new DoubleAnimation(1, 0, duration) { EasingFunction = ease };
        anim.Completed += (_, _) => tcs.TrySetResult(true);

        PageRoot.BeginAnimation(OpacityProperty, anim);

        await tcs.Task;
    }

    private static List<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var result = new List<T>();
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
                result.Add(t);
            result.AddRange(FindVisualChildren<T>(child));
        }
        return result;
    }

    private async void AnimateEpisodeButtonsEntrance()
    {
        await Task.Delay(100);

        if (!IsLoaded) return;

        var buttons = FindVisualChildren<System.Windows.Controls.Button>(PlaylistBox);
        for (int i = 0; i < buttons.Count; i++)
        {
            var btn = buttons[i];
            var delay = TimeSpan.FromMilliseconds(i * 35);

            btn.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            var st = new ScaleTransform(0.88, 0.88);
            btn.RenderTransform = st;
            btn.Opacity = 0;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(420);

            var scaleAnimX = new DoubleAnimation(0.88, 1.0, dur)
            {
                BeginTime = delay,
                EasingFunction = ease
            };
            var scaleAnimY = new DoubleAnimation(0.88, 1.0, dur)
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
            btn.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        }
    }
}
