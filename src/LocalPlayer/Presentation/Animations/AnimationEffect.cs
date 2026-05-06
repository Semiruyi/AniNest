using System;
using System.Windows;
using System.Windows.Media.Animation;
using Point = System.Windows.Point;

namespace LocalPlayer.Presentation.Animations;

public readonly struct AnimationEffect
{
    public double From { get; init; }
    public double To { get; init; }
    public int DurationMs { get; init; }
    public IEasingFunction? Easing { get; init; }

    public DoubleAnimation ToDoubleAnimation(int beginTimeMs = 0)
    {
        var anim = new DoubleAnimation(From, To, TimeSpan.FromMilliseconds(DurationMs))
        {
            EasingFunction = Easing ?? AnimationHelper.EaseOut
        };
        if (beginTimeMs > 0)
            anim.BeginTime = TimeSpan.FromMilliseconds(beginTimeMs);
        return anim;
    }
}

public readonly struct EntranceEffect
{
    public AnimationEffect Scale { get; init; }
    public AnimationEffect Opacity { get; init; }
    public Point Origin { get; init; }

    public static EntranceEffect Default => new()
    {
        Scale = new AnimationEffect { From = 0, To = 1.0, DurationMs = 300, Easing = AnimationHelper.EaseOut },
        Opacity = new AnimationEffect { From = 0, To = 1, DurationMs = 300, Easing = AnimationHelper.EaseOut },
        Origin = new Point(0.5, 0.5),
    };
}

public readonly struct ExitEffect
{
    public AnimationEffect Scale { get; init; }
    public AnimationEffect Opacity { get; init; }
    public Point Origin { get; init; }

    public static ExitEffect Default => new()
    {
        Scale = new AnimationEffect { From = 1.0, To = 0, DurationMs = 300, Easing = AnimationHelper.EaseIn },
        Opacity = new AnimationEffect { From = 1, To = 0, DurationMs = 300, Easing = AnimationHelper.EaseIn },
        Origin = new Point(0.5, 0.5),
    };
}

