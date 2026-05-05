using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Persistence;

namespace LocalPlayer.Presentation.Interop;

public class PlayerInputHandler
{
    private static readonly Logger Log = AppLog.For<PlayerInputHandler>();

    private readonly ISettingsService _settings;
    private Dictionary<Key, string> keyToAction = new();

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
        keyToAction = new Dictionary<Key, string>();
        foreach (var kv in bindings)
        {
            Log.Info($"ReloadBindings:   {kv.Key} = {kv.Value} ({(int)kv.Value})");
            if (kv.Value != Key.None)
                keyToAction[kv.Value] = kv.Key;
        }
        Log.Info($"ReloadBindings: final binding map count = {keyToAction.Count}");
        BindingsChanged?.Invoke();
    }

    public Dictionary<string, Key> GetCurrentBindings()
        => _settings.GetAllKeyBindings();

    public void SetBinding(string actionName, Key key)
    {
        _settings.SetKeyBinding(actionName, key);
        ReloadBindings();
    }

    public static List<KeyBindingInfo> GetDefaultBindings()
        => SettingsService.GetDefaultKeyBindings();

    public bool HandleKeyDown(KeyEventArgs e)
    {
        Log.Info($"HandleKeyDown: Key={e.Key}");

        if (keyToAction.Count == 0)
            ReloadBindings();

        if (!keyToAction.TryGetValue(e.Key, out var actionName))
        {
            Log.Info($"Unhandled key: {e.Key}");
            return false;
        }

        Log.Info($"Matched action: {actionName}");

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
