using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LocalPlayer.Services;

public class FullscreenManager : IDisposable
{
    private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "player.log");

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [FullscreenManager] {message}{Environment.NewLine}");
        }
        catch { }
    }

    private Form? fullscreenForm;
    private Form? mainForm;
    private Control? videoContainer;
    private Control? originalParent;
    private int originalIndex = -1;
    private DockStyle originalDock;
    private Size originalSize;
    private Point originalLocation;

    private class FullscreenForm : Form
    {
        public FullscreenForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            BackColor = Color.Black;
            KeyPreview = true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // WinForms 默认会用方向键做焦点导航，导致 KeyDown 事件不触发
            // 这里拦截所有播放相关按键，手动触发 KeyDown 事件
            bool isMediaKey = keyData == Keys.Left || keyData == Keys.Right ||
                              keyData == Keys.Up || keyData == Keys.Down ||
                              keyData == Keys.Space || keyData == Keys.F ||
                              keyData == Keys.Escape ||
                              keyData == Keys.J || keyData == Keys.L ||
                              keyData == Keys.N || keyData == Keys.P ||
                              keyData == Keys.PageDown || keyData == Keys.PageUp;

            if (isMediaKey)
            {
                var e = new KeyEventArgs(keyData);
                OnKeyDown(e);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    public bool IsFullscreen { get; private set; }

    public event EventHandler<KeyEventArgs>? KeyDown;
    public event EventHandler? Exited;

    public void EnterFullscreen(Control videoContainer)
    {
        if (IsFullscreen || videoContainer == null) return;

        Log("进入全屏模式");

        this.videoContainer = videoContainer;
        mainForm = videoContainer.FindForm();
        if (mainForm == null) return;

        originalParent = videoContainer.Parent;
        originalIndex = originalParent!.Controls.GetChildIndex(videoContainer);
        originalDock = videoContainer.Dock;
        originalSize = videoContainer.Size;
        originalLocation = videoContainer.Location;

        fullscreenForm = new FullscreenForm();
        fullscreenForm.KeyDown += FullscreenForm_KeyDown;

        originalParent.Controls.Remove(videoContainer);
        fullscreenForm.Controls.Add(videoContainer);
        videoContainer.Dock = DockStyle.Fill;

        mainForm.Hide();
        fullscreenForm.Show();

        videoContainer.Invalidate();
        videoContainer.Update();

        IsFullscreen = true;

        Log("✓ 已进入全屏");
    }

    public void ExitFullscreen()
    {
        if (!IsFullscreen || fullscreenForm == null || originalParent == null || mainForm == null)
            return;

        Log("退出全屏模式");

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

        Log("✓ 已退出全屏");
    }

    public void ToggleFullscreen(Control videoContainer)
    {
        Log($"ToggleFullscreen 被调用，当前状态 IsFullscreen={IsFullscreen}");
        if (!IsFullscreen)
            EnterFullscreen(videoContainer);
        else
            ExitFullscreen();
    }

    private void FullscreenForm_KeyDown(object? sender, KeyEventArgs e)
    {
        Log($"FullscreenForm_KeyDown: KeyCode={e.KeyCode}");
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
