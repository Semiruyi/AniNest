using System;
using System.IO;
using System.Windows.Input;

namespace LocalPlayer.Services;

public class PlayerInputHandler
{
    private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "player.log");

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [PlayerInputHandler] {message}{Environment.NewLine}");
        }
        catch { }
    }

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
        Log($"HandleKeyDown 被调用: Key={e.Key}, IsFullscreen={isFullscreen}, Handled={e.Handled}");

        if (IsFunctionKey(e.Key))
        {
            Log($"识别到功能键: {e.Key}");
        }
        else
        {
            Log($"非功能键，忽略: {e.Key}");
            return false;
        }

        switch (e.Key)
        {
            case Key.Space:
                Log("触发 TogglePlayPause");
                TogglePlayPause?.Invoke(this, EventArgs.Empty);
                return true;
            case Key.Left:
                Log("触发 SeekBackward");
                SeekBackward?.Invoke(this, EventArgs.Empty);
                return true;
            case Key.Right:
                Log("触发 SeekForward");
                SeekForward?.Invoke(this, EventArgs.Empty);
                return true;
            case Key.F:
                Log("触发 ToggleFullscreen");
                ToggleFullscreen?.Invoke(this, EventArgs.Empty);
                return true;
            case Key.Escape:
                if (isFullscreen)
                {
                    Log("触发 ExitFullscreen");
                    ExitFullscreen?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    Log("触发 Back");
                    Back?.Invoke(this, EventArgs.Empty);
                }
                return true;
            case Key.J:
                Log("触发 SeekBackward (J)");
                SeekBackward?.Invoke(this, EventArgs.Empty);
                return true;
            case Key.L:
                Log("触发 SeekForward (L)");
                SeekForward?.Invoke(this, EventArgs.Empty);
                return true;
            case Key.N:
            case Key.PageDown:
                Log($"触发 NextEpisode (Key={e.Key})");
                NextEpisode?.Invoke(this, EventArgs.Empty);
                return true;
            case Key.P:
            case Key.PageUp:
                Log($"触发 PreviousEpisode (Key={e.Key})");
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
