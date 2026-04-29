using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using LocalPlayer.Helpers;

// 消歧义：UseWindowsForms 隐式导入与 WPF 类型冲突
using Point = System.Windows.Point;
using Panel = System.Windows.Controls.Panel;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace LocalPlayer.Views;

public partial class PlayerPage
{
    // 非全屏时无操作（全屏时边缘检测在 FullscreenWindow.OnMouseMove）
    private void VideoContainer_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) { }

    // ========== 全屏切换 ==========

    private void ToggleFullscreen()
    {
        if (isFullscreen)
            ExitFullscreen();
        else
            EnterFullscreen();
    }

    // ========== 进入全屏 ==========

    private void EnterFullscreen()
    {
        if (parentWindow == null || fullscreenWindow == null) return;

        _speedPopupController?.Close();

        // 1. 记录 VideoContainer 屏幕位置（DIP），用于退出回缩动画
        var source = PresentationSource.FromVisual(VideoContainer);
        var dpiX = source!.CompositionTarget!.TransformToDevice.M11;
        var dpiY = source!.CompositionTarget!.TransformToDevice.M22;

        Point screenPos = VideoContainer.PointToScreen(new Point(0, 0));
        var fromRect = new Rect(
            screenPos.X / dpiX, screenPos.Y / dpiY,
            VideoContainer.ActualWidth, VideoContainer.ActualHeight);

        // 2. 把控制栏移入 FullscreenWindow，底部叠加
        if (controlBarOriginalParent != null)
            controlBarOriginalParent.Children.Remove(ControlBar);
        fullscreenWindow.RootGrid.Children.Add(ControlBar);
        Panel.SetZIndex(ControlBar, 1);
        ControlBar.VerticalAlignment = VerticalAlignment.Bottom;
        ControlBar.HorizontalAlignment = HorizontalAlignment.Stretch;
        ControlBar.Height = 63;
        HideFullscreenControlBar(immediate: true);

        // 3. 把选集面板移入 FullscreenWindow，右侧叠加
        if (playlistOriginalParent != null)
            playlistOriginalParent.Children.Remove(PlaylistBorder);
        fullscreenWindow.RootGrid.Children.Add(PlaylistBorder);
        Panel.SetZIndex(PlaylistBorder, 2);
        PlaylistBorder.VerticalAlignment = VerticalAlignment.Stretch;
        PlaylistBorder.HorizontalAlignment = HorizontalAlignment.Right;
        HideFullscreenPlaylist(immediate: true);

        // 4. WriteableBitmap 从主窗口切给全屏窗口，显示 + 缩放动画
        VideoImage.Source = null;
        fullscreenWindow.ShowWithAnimation(fromRect);

        isFullscreen = true;
        FullscreenIcon.Source = new BitmapImage(
            new Uri("pack://application:,,,/Resources/Icons/exitFullScreen.png"));
    }

    // ========== 退出全屏 ==========

    private void ExitFullscreen()
    {
        if (!isFullscreen || fullscreenWindow == null) return;

        isFullscreen = false;

        // 停止自动隐藏
        controlBarHideTimer.Stop();
        playlistHideTimer.Stop();

        // 1. 全屏窗口播放回缩动画并隐藏
        fullscreenWindow.HideWithAnimation();

        // 2. 恢复选集面板到 PageRoot
        fullscreenWindow.RootGrid.Children.Remove(PlaylistBorder);
        if (playlistOriginalParent != null)
        {
            if (playlistOriginalIndex >= 0 && playlistOriginalIndex <= playlistOriginalParent.Children.Count)
                playlistOriginalParent.Children.Insert(playlistOriginalIndex, PlaylistBorder);
            else
                playlistOriginalParent.Children.Add(PlaylistBorder);
        }
        Grid.SetRow(PlaylistBorder, 0);
        Grid.SetRowSpan(PlaylistBorder, 2);
        Grid.SetColumn(PlaylistBorder, 1);
        PlaylistBorder.VerticalAlignment = VerticalAlignment.Stretch;
        PlaylistBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
        PlaylistBorder.BeginAnimation(UIElement.OpacityProperty, null);
        PlaylistBorder.Opacity = 1;
        PlaylistBorder.IsHitTestVisible = true;

        // 3. 恢复控制栏到 PageRoot
        fullscreenWindow.RootGrid.Children.Remove(ControlBar);
        if (controlBarOriginalParent != null)
        {
            if (controlBarOriginalIndex >= 0 && controlBarOriginalIndex <= controlBarOriginalParent.Children.Count)
                controlBarOriginalParent.Children.Insert(controlBarOriginalIndex, ControlBar);
            else
                controlBarOriginalParent.Children.Add(ControlBar);
        }
        Grid.SetRow(ControlBar, 1);
        Grid.SetRowSpan(ControlBar, 1);
        Panel.SetZIndex(ControlBar, 0);
        ControlBar.VerticalAlignment = VerticalAlignment.Stretch;
        ControlBar.BeginAnimation(UIElement.OpacityProperty, null);
        ControlBar.Visibility = Visibility.Visible;
        ControlBar.Opacity = 1;
        ControlBar.IsHitTestVisible = true;

        // 4. WriteableBitmap 切回主窗口
        VideoImage.Source = mediaController.VideoBitmap;

        FullscreenIcon.Source = new BitmapImage(
            new Uri("pack://application:,,,/Resources/Icons/fullScreen.png"));
    }

    // ========== 控制栏显隐 ==========

    private void ControlBar_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!isFullscreen) return;
        controlBarHideTimer.Stop();
        AnimateOpacity(ControlBar, 1);
    }

    private void ControlBar_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!isFullscreen) return;
        controlBarHideTimer.Start();
    }

    private void ControlBarHideTimer_Tick(object? sender, EventArgs e)
    {
        if (!ControlBar.IsMouseOver)
            HideFullscreenControlBar();
    }

    private void ShowFullscreenControlBar()
    {
        controlBarHideTimer.Stop();
        if (ControlBar.IsHitTestVisible) return;
        ControlBar.Visibility = Visibility.Visible;
        ControlBar.IsHitTestVisible = true;
        AnimateOpacity(ControlBar, 1);
        Log($"ShowFullscreenControlBar: 切换焦点到 ControlBar, 之前焦点={Keyboard.FocusedElement?.GetType().Name}");
        Keyboard.Focus(ControlBar);
    }

    private void HideFullscreenControlBar(bool immediate = false)
    {
        if (immediate)
        {
            ControlBar.BeginAnimation(UIElement.OpacityProperty, null);
            ControlBar.Opacity = 0;
            ControlBar.IsHitTestVisible = false;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(200);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var anim = new DoubleAnimation(ControlBar.Opacity, 0, duration) { EasingFunction = ease };
        anim.Completed += (_, _) =>
        {
            if (isFullscreen)
                ControlBar.IsHitTestVisible = false;
        };
        ControlBar.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    // ========== 选集面板显隐 ==========

    private void PlaylistBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!isFullscreen) return;
        playlistHideTimer.Stop();
        AnimateOpacity(PlaylistBorder, 1);
    }

    private void PlaylistBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!isFullscreen) return;
        playlistHideTimer.Start();
    }

    private void PlaylistHideTimer_Tick(object? sender, EventArgs e)
    {
        if (!PlaylistBorder.IsMouseOver)
            HideFullscreenPlaylist();
    }

    private void ShowFullscreenPlaylist()
    {
        playlistHideTimer.Stop();
        if (PlaylistBorder.IsHitTestVisible) return;
        PlaylistBorder.IsHitTestVisible = true;
        AnimateOpacity(PlaylistBorder, 1);
    }

    private void HideFullscreenPlaylist(bool immediate = false)
    {
        if (immediate)
        {
            PlaylistBorder.BeginAnimation(UIElement.OpacityProperty, null);
            PlaylistBorder.Opacity = 0;
            PlaylistBorder.IsHitTestVisible = false;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(200);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var anim = new DoubleAnimation(PlaylistBorder.Opacity, 0, duration) { EasingFunction = ease };
        anim.Completed += (_, _) =>
        {
            if (isFullscreen)
                PlaylistBorder.IsHitTestVisible = false;
        };
        PlaylistBorder.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    // ========== 透明度动画 ==========

    private static void AnimateOpacity(UIElement element, double target, int durationMs = 200)
        => AnimationHelper.AnimateFromCurrent(element, UIElement.OpacityProperty, target, durationMs);
}
