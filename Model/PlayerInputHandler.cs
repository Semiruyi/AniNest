using System;
using System.Collections.Generic;
using System.IO;
using WinKey = System.Windows.Input.Key;
using WinKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace LocalPlayer.Model;

public class PlayerInputHandler
{
    private static void Log(string message) => AppLog.Info(nameof(PlayerInputHandler), message);
    private static void LogError(string message, Exception? ex = null) => AppLog.Error(nameof(PlayerInputHandler), message, ex);

    private readonly ISettingsService _settings;
    private Dictionary<WinKey, string> keyToAction = new();

    public event EventHandler? TogglePlayPause;
    public event EventHandler? SeekForward;
    public event EventHandler? SeekBackward;
    public event EventHandler? Back;
    public event EventHandler? NextEpisode;
    public event EventHandler? PreviousEpisode;

    public PlayerInputHandler(ISettingsService settings)
    {
        _settings = settings;
    }

    public void ReloadBindings()
    {
        Log("ReloadBindings: 开始重新加载快捷键...");
        _settings.Reload();
        var bindings = _settings.GetAllKeyBindings();
        Log($"ReloadBindings: GetAllKeyBindings 返回 {bindings.Count} 个绑定");
        keyToAction = new Dictionary<WinKey, string>();
        foreach (var kv in bindings)
        {
            Log($"ReloadBindings:   {kv.Key} = {kv.Value} ({(int)kv.Value})");
            if (kv.Value != WinKey.None)
                keyToAction[kv.Value] = kv.Key;
        }
        Log($"ReloadBindings: 最终加载了 {keyToAction.Count} 个快捷键到映射表");
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
        Log($"HandleKeyDown: Key={e.Key}");

        if (keyToAction.Count == 0)
            ReloadBindings();

        if (!keyToAction.TryGetValue(e.Key, out var actionName))
        {
            Log($"未绑定的按键: {e.Key}");
            return false;
        }

        Log($"匹配到动作: {actionName}");

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
