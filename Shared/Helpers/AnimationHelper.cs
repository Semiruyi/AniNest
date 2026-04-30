using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Point = System.Windows.Point;

namespace LocalPlayer.Shared.Helpers;

public static class AnimationHelper
{
    private static IEasingFunction? _easeInOut;
    private static IEasingFunction? _easeOut;
    private static IEasingFunction? _easeIn;

    public static IEasingFunction EaseInOut => _easeInOut ??= new CubicEase { EasingMode = EasingMode.EaseInOut };
    public static IEasingFunction EaseOut => _easeOut ??= new CubicEase { EasingMode = EasingMode.EaseOut };
    public static IEasingFunction EaseIn => _easeIn ??= new CubicEase { EasingMode = EasingMode.EaseIn };

    public static DoubleAnimation CreateAnim(double from, double to, int durationMs, IEasingFunction? ease = null, int beginTimeMs = 0)
    {
        var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = ease ?? EaseInOut
        };
        if (beginTimeMs > 0)
            anim.BeginTime = TimeSpan.FromMilliseconds(beginTimeMs);
        return anim;
    }

    public static DoubleAnimation CreateAnim(double to, int durationMs, IEasingFunction? ease = null)
    {
        return new DoubleAnimation(to, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = ease ?? EaseInOut
        };
    }

    // ========== 通用依赖属性动画 ==========

    public static void Animate(IAnimatable target, DependencyProperty property,
        double from, double to, int durationMs, IEasingFunction? ease = null, Action? onCompleted = null)
    {
        target.BeginAnimation(property, null);
        var dobj = (DependencyObject)target;
        var anim = CreateAnim(from, to, durationMs, ease);
        if (onCompleted != null)
            anim.Completed += (_, _) => onCompleted();
        dobj.SetValue(property, from);
        target.BeginAnimation(property, anim);
    }

    public static Task AnimateAsync(IAnimatable target, DependencyProperty property,
        double from, double to, int durationMs, IEasingFunction? ease = null)
    {
        var tcs = new TaskCompletionSource<bool>();
        Animate(target, property, from, to, durationMs, ease, () => tcs.TrySetResult(true));
        return tcs.Task;
    }

    public static void AnimateFromCurrent(IAnimatable target, DependencyProperty property,
        double to, int durationMs, IEasingFunction? ease = null, Action? onCompleted = null)
    {
        target.BeginAnimation(property, null);
        var current = (double)((DependencyObject)target).GetValue(property);
        Animate(target, property, current, to, durationMs, ease, onCompleted);
    }

    public static Task AnimateFromCurrentAsync(IAnimatable target, DependencyProperty property,
        double to, int durationMs, IEasingFunction? ease = null)
    {
        var tcs = new TaskCompletionSource<bool>();
        AnimateFromCurrent(target, property, to, durationMs, ease, () => tcs.TrySetResult(true));
        return tcs.Task;
    }

    // ========== 透明度 ==========

    public static Task FadeInAsync(UIElement element, int durationMs = 300, IEasingFunction? ease = null)
    {
        element.Visibility = Visibility.Visible;
        return AnimateAsync(element, UIElement.OpacityProperty, 0, 1, durationMs, ease);
    }

    public static Task FadeOutAsync(UIElement element, int durationMs = 300,
        IEasingFunction? ease = null, Action? onCompleted = null)
    {
        var tcs = new TaskCompletionSource<bool>();
        AnimateFromCurrent(element, UIElement.OpacityProperty, 0, durationMs, ease, () =>
        {
            onCompleted?.Invoke();
            tcs.TrySetResult(true);
        });
        return tcs.Task;
    }

    // ========== ScaleTransform ==========

    public static void EnsureScaleTransform(FrameworkElement element)
    {
        if (element.RenderTransform is not ScaleTransform)
        {
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            element.RenderTransform = new ScaleTransform(1, 1);
        }
    }

    public static void AnimateScaleTransform(ScaleTransform transform, double to,
        int durationMs, IEasingFunction? ease = null)
    {
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        var d = TimeSpan.FromMilliseconds(durationMs);
        var e = ease ?? EaseInOut;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(to, d) { EasingFunction = e });
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(to, d) { EasingFunction = e });
    }

    public static void AnimateScaleTransform(ScaleTransform transform, double toX, double toY,
        int durationMs, IEasingFunction? ease = null)
    {
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        var d = TimeSpan.FromMilliseconds(durationMs);
        var e = ease ?? EaseInOut;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(toX, d) { EasingFunction = e });
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(toY, d) { EasingFunction = e });
    }

    // ========== TranslateTransform ==========

    public static void AnimateTranslate(TranslateTransform transform, double toX, double toY,
        int durationMs, IEasingFunction? ease = null, Action? onCompleted = null)
    {
        var d = TimeSpan.FromMilliseconds(durationMs);
        var e = ease ?? EaseInOut;
        var animX = new DoubleAnimation(toX, d) { EasingFunction = e };
        var animY = new DoubleAnimation(toY, d) { EasingFunction = e };
        if (onCompleted != null)
            animX.Completed += (_, _) => onCompleted();
        transform.BeginAnimation(TranslateTransform.XProperty, animX);
        transform.BeginAnimation(TranslateTransform.YProperty, animY);
    }
}
