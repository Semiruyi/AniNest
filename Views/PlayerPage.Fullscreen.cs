using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace LocalPlayer.Views;

public partial class PlayerPage
{
    private void ToggleFullscreen()
    {
        Log($"ToggleFullscreen 被调用，当前状态 isFullscreen={isFullscreen}");
        if (isFullscreen)
            ExitFullscreen();
        else
            EnterFullscreen();
    }

    private void EnterFullscreen()
    {
        if (parentWindow == null)
        {
            Log("EnterFullscreen 失败: parentWindow 为 null");
            return;
        }

        Log("进入全屏模式");
        savedWindowState = parentWindow.WindowState;
        savedWindowStyle = parentWindow.WindowStyle;
        savedResizeMode = parentWindow.ResizeMode;

        parentWindow.WindowStyle = WindowStyle.None;
        parentWindow.ResizeMode = ResizeMode.NoResize;
        parentWindow.WindowState = WindowState.Maximized;

        // 选集面板移入视频区，右侧对齐
        if (playlistOriginalParent != null)
            playlistOriginalParent.Children.Remove(PlaylistBorder);
        VideoContainer.Children.Add(PlaylistBorder);
        System.Windows.Controls.Panel.SetZIndex(PlaylistBorder, 2);
        PlaylistBorder.VerticalAlignment = VerticalAlignment.Stretch;
        PlaylistBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
        HideFullscreenPlaylist(immediate: true);

        // 控制栏移入视频区，底部对齐
        if (controlBarOriginalParent != null)
            controlBarOriginalParent.Children.Remove(ControlBar);
        VideoContainer.Children.Add(ControlBar);
        System.Windows.Controls.Panel.SetZIndex(ControlBar, 1);

        ControlBar.VerticalAlignment = VerticalAlignment.Bottom;
        ControlBar.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
        ControlBar.Height = 63;

        HideFullscreenControlBar(immediate: true);

        VideoContainer.MouseMove -= VideoContainer_MouseMove;
        VideoContainer.MouseMove += VideoContainer_MouseMove;

        FullscreenIcon.Source = new BitmapImage(
            new Uri("pack://application:,,,/Resources/Icons/exitFullScreen.png"));

        isFullscreen = true;

        Keyboard.Focus(ControlBar);
        Log("✓ 已进入全屏");
    }

    private void ExitFullscreen()
    {
        if (!isFullscreen || parentWindow == null)
        {
            Log($"ExitFullscreen 提前返回: isFullscreen={isFullscreen}, parentWindow={parentWindow}");
            return;
        }

        Log("退出全屏模式");
        parentWindow.WindowState = savedWindowState;
        parentWindow.WindowStyle = savedWindowStyle;
        parentWindow.ResizeMode = savedResizeMode;

        // 恢复选集面板
        VideoContainer.Children.Remove(PlaylistBorder);
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
        PlaylistBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
        PlaylistBorder.BeginAnimation(UIElement.OpacityProperty, null);
        PlaylistBorder.Opacity = 1;
        PlaylistBorder.IsHitTestVisible = true;

        // 恢复控制栏
        VideoContainer.Children.Remove(ControlBar);
        if (controlBarOriginalParent != null)
        {
            if (controlBarOriginalIndex >= 0 && controlBarOriginalIndex <= controlBarOriginalParent.Children.Count)
                controlBarOriginalParent.Children.Insert(controlBarOriginalIndex, ControlBar);
            else
                controlBarOriginalParent.Children.Add(ControlBar);
        }

        Grid.SetRow(ControlBar, 1);
        Grid.SetRowSpan(ControlBar, 1);
        ControlBar.VerticalAlignment = VerticalAlignment.Stretch;
        System.Windows.Controls.Panel.SetZIndex(ControlBar, 0);

        ControlBar.Visibility = Visibility.Visible;
        ControlBar.Opacity = 1;
        ControlBar.IsHitTestVisible = true;

        controlBarHideTimer.Stop();
        playlistHideTimer.Stop();
        VideoContainer.MouseMove -= VideoContainer_MouseMove;

        FullscreenIcon.Source = new BitmapImage(
            new Uri("pack://application:,,,/Resources/Icons/fullScreen.png"));

        isFullscreen = false;
        Log("✓ 已退出全屏");
    }

    private void VideoContainer_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!isFullscreen) return;
        var pos = e.GetPosition(VideoContainer);
        if (pos.Y > VideoContainer.ActualHeight - 10)
        {
            ShowFullscreenControlBar();
        }
        if (pos.X > VideoContainer.ActualWidth - 10)
        {
            ShowFullscreenPlaylist();
        }
    }

    // ========== 控制栏 ==========

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
        {
            HideFullscreenControlBar();
        }
    }

    private void ShowFullscreenControlBar()
    {
        controlBarHideTimer.Stop();
        if (ControlBar.IsHitTestVisible) return;
        ControlBar.Visibility = Visibility.Visible;
        ControlBar.IsHitTestVisible = true;
        AnimateOpacity(ControlBar, 1);
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
            ControlBar.IsHitTestVisible = false;
        };
        ControlBar.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    // ========== 选集面板 ==========

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
        {
            HideFullscreenPlaylist();
        }
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
            PlaylistBorder.IsHitTestVisible = false;
        };
        PlaylistBorder.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    // ========== 透明度动画 ==========

    private static void AnimateOpacity(UIElement element, double target, int durationMs = 200)
    {
        var currentOpacity = element.Opacity;
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = currentOpacity;
        var duration = TimeSpan.FromMilliseconds(durationMs);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var anim = new DoubleAnimation(currentOpacity, target, duration) { EasingFunction = ease };
        element.BeginAnimation(UIElement.OpacityProperty, anim);
    }
}
