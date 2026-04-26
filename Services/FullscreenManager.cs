using System;
using System.Drawing;
using System.Windows.Forms;

namespace LocalPlayer.Services;

public class FullscreenManager : IDisposable
{
    private Form? fullscreenForm;
    private Form? mainForm;
    private Control? videoContainer;
    private Control? originalParent;
    private int originalIndex = -1;
    private DockStyle originalDock;
    private Size originalSize;
    private Point originalLocation;

    public bool IsFullscreen { get; private set; }

    public event EventHandler<KeyEventArgs>? KeyDown;
    public event EventHandler? Exited;

    public void EnterFullscreen(Control videoContainer)
    {
        if (IsFullscreen || videoContainer == null) return;

        Console.WriteLine("[FullscreenManager] 进入全屏模式");

        this.videoContainer = videoContainer;
        mainForm = videoContainer.FindForm();
        if (mainForm == null) return;

        originalParent = videoContainer.Parent;
        originalIndex = originalParent!.Controls.GetChildIndex(videoContainer);
        originalDock = videoContainer.Dock;
        originalSize = videoContainer.Size;
        originalLocation = videoContainer.Location;

        fullscreenForm = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            WindowState = FormWindowState.Maximized,
            TopMost = true,
            BackColor = Color.Black,
            KeyPreview = true
        };

        fullscreenForm.KeyDown += FullscreenForm_KeyDown;

        originalParent.Controls.Remove(videoContainer);
        fullscreenForm.Controls.Add(videoContainer);
        videoContainer.Dock = DockStyle.Fill;

        mainForm.Hide();
        fullscreenForm.Show();

        videoContainer.Invalidate();
        videoContainer.Update();

        IsFullscreen = true;

        Console.WriteLine("[FullscreenManager] ✓ 已进入全屏");
    }

    public void ExitFullscreen()
    {
        if (!IsFullscreen || fullscreenForm == null || originalParent == null || mainForm == null)
            return;

        Console.WriteLine("[FullscreenManager] 退出全屏模式");

        if (videoContainer != null)
        {
            fullscreenForm.Controls.Remove(videoContainer);
            originalParent.Controls.Add(videoContainer);
            originalParent.Controls.SetChildIndex(videoContainer, originalIndex);

            videoContainer.Dock = originalDock;
            videoContainer.Size = originalSize;
            videoContainer.Location = originalLocation;

            videoContainer.Invalidate();
            videoContainer.Update();
        }

        fullscreenForm.Close();
        fullscreenForm.Dispose();
        fullscreenForm = null;

        mainForm.Show();
        mainForm.Focus();

        IsFullscreen = false;
        Exited?.Invoke(this, EventArgs.Empty);

        Console.WriteLine("[FullscreenManager] ✓ 已退出全屏");
    }

    public void ToggleFullscreen(Control videoContainer)
    {
        if (!IsFullscreen)
            EnterFullscreen(videoContainer);
        else
            ExitFullscreen();
    }

    private void FullscreenForm_KeyDown(object? sender, KeyEventArgs e)
    {
        KeyDown?.Invoke(this, e);
    }

    public void Dispose()
    {
        if (IsFullscreen)
        {
            ExitFullscreen();
        }
    }
}
