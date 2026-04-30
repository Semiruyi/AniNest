using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LocalPlayer.Media;
using LocalPlayer.Primitives;

namespace LocalPlayer.Interaction;

/// <summary>
/// 暂停大图标动画控制器，PlayerPage 和 FullscreenWindow 共用。
/// </summary>
public class PauseOverlayController
{
    private readonly ScaleTransform _scale;
    private readonly UIElement _icon;

    public PauseOverlayController(ScaleTransform scale, UIElement icon)
    {
        _scale = scale;
        _icon = icon;
    }

    /// <summary>暂停 → 显示图标（scale 0→1, opacity 0→1）</summary>
    public void AnimateIn()
    {
        _scale.ScaleX = 0;
        _scale.ScaleY = 0;
        _icon.Opacity = 0;
        AnimationHelper.AnimateScaleTransform(_scale, 1, 250, AnimationHelper.EaseOut);
        AnimationHelper.Animate(_icon, UIElement.OpacityProperty, 0, 1, 250, AnimationHelper.EaseOut);
    }

    /// <summary>播放 → 隐藏图标（scale →0, opacity →0）</summary>
    public void AnimateOut()
    {
        AnimationHelper.AnimateScaleTransform(_scale, 0, 180, AnimationHelper.EaseIn);
        AnimationHelper.AnimateFromCurrent(_icon, UIElement.OpacityProperty, 0, 180, AnimationHelper.EaseIn);
    }

    /// <summary>立即显示，取消所有动画（全屏进入时如果当前已暂停）</summary>
    public void ShowImmediate()
    {
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _icon.BeginAnimation(UIElement.OpacityProperty, null);
        _scale.ScaleX = 1;
        _scale.ScaleY = 1;
        _icon.Opacity = 1;
    }

    /// <summary>关联 MediaPlayerController 事件，自动响应播放/暂停/停止</summary>
    public void WireMediaEvents(MediaPlayerController media, Dispatcher dispatcher)
    {
        media.Playing += (_, _) => dispatcher.Invoke(AnimateOut);
        media.Paused  += (_, _) => dispatcher.Invoke(AnimateIn);
        media.Stopped += (_, _) => dispatcher.Invoke(AnimateOut);
    }
}
