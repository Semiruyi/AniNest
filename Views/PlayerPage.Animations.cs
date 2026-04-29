using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LocalPlayer.Helpers;

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
            var delayMs = i * 35;

            btn.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            var st = new ScaleTransform(0.88, 0.88);
            btn.RenderTransform = st;
            btn.Opacity = 0;

            st.BeginAnimation(ScaleTransform.ScaleXProperty,
                AnimationHelper.CreateAnim(0.88, 1.0, 420, AnimationHelper.EaseOut, delayMs));
            st.BeginAnimation(ScaleTransform.ScaleYProperty,
                AnimationHelper.CreateAnim(0.88, 1.0, 420, AnimationHelper.EaseOut, delayMs));
            btn.BeginAnimation(UIElement.OpacityProperty,
                AnimationHelper.CreateAnim(0, 1, 320, beginTimeMs: delayMs));
        }
    }
}
