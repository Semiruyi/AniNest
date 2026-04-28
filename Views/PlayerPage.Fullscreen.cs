using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        PlaylistBorder.Visibility = Visibility.Collapsed;

        if (controlBarOriginalParent != null)
            controlBarOriginalParent.Children.Remove(ControlBar);
        VideoContainer.Children.Add(ControlBar);
        System.Windows.Controls.Panel.SetZIndex(ControlBar, 1);

        ControlBar.VerticalAlignment = VerticalAlignment.Bottom;
        ControlBar.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
        ControlBar.Height = 63;

        HideFullscreenControlBar();

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

        PlaylistBorder.Visibility = Visibility.Visible;

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
    }

    private void ControlBar_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!isFullscreen) return;
        controlBarHideTimer.Stop();
        ControlBar.Opacity = 1;
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
        ControlBar.Visibility = Visibility.Visible;
        ControlBar.Opacity = 1;
        ControlBar.IsHitTestVisible = true;
        Keyboard.Focus(ControlBar);
    }

    private void HideFullscreenControlBar()
    {
        ControlBar.Opacity = 0;
        ControlBar.IsHitTestVisible = false;
    }
}
