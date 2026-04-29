using System;
using System.Threading.Tasks;
using System.Windows;
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

        PageRoot.BeginAnimation(UIElement.OpacityProperty, anim);

        await tcs.Task;
    }
}
