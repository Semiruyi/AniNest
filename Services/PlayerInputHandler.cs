using System;
using System.Windows.Forms;

namespace LocalPlayer.Services;

public class PlayerInputHandler
{
    public event EventHandler? TogglePlayPause;
    public event EventHandler? SeekForward;
    public event EventHandler? SeekBackward;
    public event EventHandler? ToggleFullscreen;
    public event EventHandler? ExitFullscreen;
    public event EventHandler? Back;
    public event EventHandler? NextEpisode;
    public event EventHandler? PreviousEpisode;

    public bool HandleKeyDown(KeyEventArgs e, bool isFullscreen)
    {
        if (IsFunctionKey(e.KeyCode))
        {
            Console.WriteLine($"[PlayerInputHandler] 处理按键: {e.KeyCode}");
        }

        switch (e.KeyCode)
        {
            case Keys.Space:
                TogglePlayPause?.Invoke(this, EventArgs.Empty);
                return true;
            case Keys.Left:
                SeekBackward?.Invoke(this, EventArgs.Empty);
                return true;
            case Keys.Right:
                SeekForward?.Invoke(this, EventArgs.Empty);
                return true;
            case Keys.F:
                ToggleFullscreen?.Invoke(this, EventArgs.Empty);
                return true;
            case Keys.Escape:
                if (isFullscreen)
                    ExitFullscreen?.Invoke(this, EventArgs.Empty);
                else
                    Back?.Invoke(this, EventArgs.Empty);
                return true;
            case Keys.J:
                SeekBackward?.Invoke(this, EventArgs.Empty);
                return true;
            case Keys.L:
                SeekForward?.Invoke(this, EventArgs.Empty);
                return true;
            case Keys.N:
            case Keys.PageDown:
                NextEpisode?.Invoke(this, EventArgs.Empty);
                return true;
            case Keys.P:
            case Keys.PageUp:
                PreviousEpisode?.Invoke(this, EventArgs.Empty);
                return true;
            default:
                return false;
        }
    }

    private static bool IsFunctionKey(Keys keyCode)
    {
        return keyCode == Keys.Left || keyCode == Keys.Right ||
               keyCode == Keys.Space || keyCode == Keys.F ||
               keyCode == Keys.Escape ||
               keyCode == Keys.J || keyCode == Keys.L ||
               keyCode == Keys.N || keyCode == Keys.P ||
               keyCode == Keys.PageUp || keyCode == Keys.PageDown;
    }
}
