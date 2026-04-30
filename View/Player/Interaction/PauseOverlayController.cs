using System.Windows;
using System.Windows.Media;
using LocalPlayer.View.Animations;

namespace LocalPlayer.View.Player.Interaction;

/// <summary>
/// 暂停大图标动画控制器，PlayerPage 和 FullscreenWindow 共用。
/// </summary>
public class PauseOverlayController
{
    private readonly PopupAnimator _animator;

    public PauseOverlayController(ScaleTransform scale, UIElement icon)
    {
        _animator = new PopupAnimator(scale, icon, showDurationMs: 250, hideDurationMs: 180);
    }

    public void OnPlaying() => _animator.Hide();
    public void OnPaused() => _animator.Show();
    public void OnStopped() => _animator.Hide();
    public void ShowImmediate() => _animator.ShowImmediate();
}
