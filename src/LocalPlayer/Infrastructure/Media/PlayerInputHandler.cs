using System;
using System.Collections.Generic;
using System.IO;
using WinKey = System.Windows.Input.Key;
using WinKeyEventArgs = System.Windows.Input.KeyEventArgs;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using LocalPlayer.Infrastructure.Interop;
namespace LocalPlayer.Infrastructure.Media;

public class PlayerInputHandler
{
    private static readonly Logger Log = AppLog.For<PlayerInputHandler>();

    private readonly ISettingsService _settings;
    private Dictionary<WinKey, string> keyToAction = new();

    public event EventHandler? TogglePlayPause;
    public event EventHandler? SeekForward;
    public event EventHandler? SeekBackward;
    public event EventHandler? Back;
    public event EventHandler? NextEpisode;
    public event EventHandler? PreviousEpisode;
    public event Action? BindingsChanged;

    public PlayerInputHandler(ISettingsService settings)
    {
        _settings = settings;
    }

    public void ReloadBindings()
    {
        Log.Info("ReloadBindings: reloading key bindings...");
        _settings.Reload();
        var bindings = _settings.GetAllKeyBindings();
        Log.Info($"ReloadBindings: GetAllKeyBindings returned {bindings.Count} bindings");
        keyToAction = new Dictionary<WinKey, string>();
        foreach (var kv in bindings)
        {
            Log.Info($"ReloadBindings:   {kv.Key} = {kv.Value} ({(int)kv.Value})");
            if (kv.Value != WinKey.None)
                keyToAction[kv.Value] = kv.Key;
        }
        Log.Info($"ReloadBindings: 鏈€缁堝姞杞戒簡 {keyToAction.Count} 涓揩鎹烽敭鍒版槧灏勮〃");
        BindingsChanged?.Invoke();
    }

    public Dictionary<string, WinKey> GetCurrentBindings()
    {
        return _settings.GetAllKeyBindings();
    }

    public void SetBinding(string actionName, WinKey key)
    {
        _settings.SetKeyBinding(actionName, key);
        ReloadBindings();
    }

    public static List<KeyBindingInfo> GetDefaultBindings()
    {
        return SettingsService.GetDefaultKeyBindings();
    }

    public bool HandleKeyDown(WinKeyEventArgs e)
    {
        Log.Info($"HandleKeyDown: Key={e.Key}");

        if (keyToAction.Count == 0)
            ReloadBindings();

        if (!keyToAction.TryGetValue(e.Key, out var actionName))
        {
            Log.Info($"鏈粦瀹氱殑鎸夐敭: {e.Key}");
            return false;
        }

        Log.Info($"鍖归厤鍒板姩浣? {actionName}");

        switch (actionName)
        {
            case "TogglePlayPause":
                TogglePlayPause?.Invoke(this, EventArgs.Empty);
                return true;
            case "SeekBackward":
            case "SeekBackwardAlt":
                SeekBackward?.Invoke(this, EventArgs.Empty);
                return true;
            case "SeekForward":
            case "SeekForwardAlt":
                SeekForward?.Invoke(this, EventArgs.Empty);
                return true;
            case "Back":
                Back?.Invoke(this, EventArgs.Empty);
                return true;
            case "NextEpisode":
                NextEpisode?.Invoke(this, EventArgs.Empty);
                return true;
            case "PreviousEpisode":
                PreviousEpisode?.Invoke(this, EventArgs.Empty);
                return true;
            default:
                return false;
        }
    }
}



