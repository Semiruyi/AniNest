using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Point = System.Windows.Point;

namespace LocalPlayer.View.Animations;

public static class AnimationHelper
{
    private static IEasingFunction? _easeInOut;
    private static IEasingFunction? _easeOut;
    private static IEasingFunction? _easeIn;

    public static IEasingFunction EaseInOut => _easeInOut ??= new CubicEase { EasingMode = EasingMode.EaseInOut };
    public static IEasingFunction EaseOut => _easeOut ??= new CubicEase { EasingMode = EasingMode.EaseOut };

    // 删除等退出动画用 Material 标准 Accelerate 曲线 (0.4,0,1,1),
    // t=0.5 时进度 35%, 比原生 CubicEase-In (12.5%) 启动快得多
    public static IEasingFunction EaseIn => _easeIn ??= new CubicBezierEase { X1 = 0.4, Y1 = 0, X2 = 1, Y2 = 1 };

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
        var current = (double)((DependencyObject)target).GetValue(property);
        target.BeginAnimation(property, null);
        Animate(target, property, current, to, durationMs, ease, onCompleted);
    }

    public static Task AnimateFromCurrentAsync(IAnimatable target, DependencyProperty property,
        double to, int durationMs, IEasingFunction? ease = null)
    {
        var tcs = new TaskCompletionSource<bool>();
        AnimateFromCurrent(target, property, to, durationMs, ease, () => tcs.TrySetResult(true));
        return tcs.Task;
    }

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
        double fromX = transform.ScaleX;
        double fromY = transform.ScaleY;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        var d = TimeSpan.FromMilliseconds(durationMs);
        var e = ease ?? EaseInOut;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(fromX, to, d) { EasingFunction = e });
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(fromY, to, d) { EasingFunction = e });
    }

    public static void AnimateScaleTransform(ScaleTransform transform, double toX, double toY,
        int durationMs, IEasingFunction? ease = null)
    {
        double fromX = transform.ScaleX;
        double fromY = transform.ScaleY;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        var d = TimeSpan.FromMilliseconds(durationMs);
        var e = ease ?? EaseInOut;
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(fromX, toX, d) { EasingFunction = e });
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(fromY, toY, d) { EasingFunction = e });
    }

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

    public static void ApplyEntrance(UIElement element, EntranceEffect effect, int beginTimeMs = 0)
    {
        element.RenderTransformOrigin = effect.Origin;
        element.RenderTransform = new ScaleTransform(effect.Scale.From, effect.Scale.From);
        element.Opacity = effect.Opacity.From;

        var scale = (ScaleTransform)element.RenderTransform;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, effect.Scale.ToDoubleAnimation(beginTimeMs));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, effect.Scale.ToDoubleAnimation(beginTimeMs));

        var opacityAnim = effect.Opacity.ToDoubleAnimation(beginTimeMs);
        opacityAnim.Completed += (_, _) => element.Opacity = effect.Opacity.To;
        element.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
    }
}
