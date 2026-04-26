using System;
using System.Windows.Input;

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

    public bool HandleKeyDown(System.Windows.Input.KeyEventArgs e, bool isFullscreen)
    {
        if (IsFunctionKey(e.Key))
        {
            Console.WriteLine($"[PlayerInputHandler] 处理按键: {e.Key}");
        }

        switch (e.Key)
        {
            case Key.Space:
                TogglePlayPause?.Invoke(this, EventArgs.Empty);
                return true;
            case Key.Left:
                SeekBackward?.Invoke(this, EventArgs.Empty);
                return true;
            case Key.Right:
                SeekForward?.Invoke(this, EventArgs.Empty);
                return true;
            case Key.F:
                ToggleFullscreen?.Invoke(this, EventArgs.Empty);
                return true;
            case Key.Escape:
                if (isFullscreen)
                    ExitFullscreen?.Invoke(this, EventArgs.Empty);
                else
                    Back?.Invoke(this, EventArgs.Empty);
                return true;
            case Key.J:
                SeekBackward?.Invoke(this, EventArgs.Empty);
                return true;
            case Key.L:
                SeekForward?.Invoke(this, EventArgs.Empty);
                return true;
            case Key.N:
            case Key.PageDown:
                NextEpisode?.Invoke(this, EventArgs.Empty);
                return true;
            case Key.P:
            case Key.PageUp:
                PreviousEpisode?.Invoke(this, EventArgs.Empty);
                return true;
            default:
                return false;
        }
    }

    private static bool IsFunctionKey(Key key)
    {
        return key == Key.Left || key == Key.Right ||
               key == Key.Space || key == Key.F ||
               key == Key.Escape ||
               key == Key.J || key == Key.L ||
               key == Key.N || key == Key.P ||
               key == Key.PageUp || key == Key.PageDown;
    }
}
