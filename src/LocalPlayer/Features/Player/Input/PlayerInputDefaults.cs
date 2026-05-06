using System.Windows.Input;

namespace LocalPlayer.Features.Player.Input;

public static class PlayerInputDefaults
{
    public static PlayerInputProfile Create()
    {
        var profile = new PlayerInputProfile();
        var bindings = profile.Bindings;

        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.PlayPause,
            KeyTrigger = new PlayerKeyTrigger { Key = Key.Space, AllowRepeat = false }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.Stop,
            KeyTrigger = new PlayerKeyTrigger { Key = Key.S, AllowRepeat = false }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.SeekBackwardSmall,
            KeyTrigger = new PlayerKeyTrigger { Key = Key.Left }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.SeekForwardSmall,
            KeyTrigger = new PlayerKeyTrigger { Key = Key.Right }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.SeekBackwardLarge,
            KeyTrigger = new PlayerKeyTrigger { Key = Key.Left, Modifiers = ModifierKeys.Control }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.SeekForwardLarge,
            KeyTrigger = new PlayerKeyTrigger { Key = Key.Right, Modifiers = ModifierKeys.Control }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.Previous,
            KeyTrigger = new PlayerKeyTrigger { Key = Key.PageUp, AllowRepeat = false }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.Next,
            KeyTrigger = new PlayerKeyTrigger { Key = Key.PageDown, AllowRepeat = false }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.ToggleFullscreen,
            KeyTrigger = new PlayerKeyTrigger { Key = Key.Enter, AllowRepeat = false }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.ExitFullscreenOrBack,
            KeyTrigger = new PlayerKeyTrigger { Key = Key.Escape, AllowRepeat = false }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.TogglePlaylist,
            KeyTrigger = new PlayerKeyTrigger { Key = Key.L, AllowRepeat = false }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.ToggleFullscreen,
            MouseTrigger = new PlayerMouseTrigger { Button = MouseButton.Left, Kind = PlayerInputTriggerKind.MouseDoubleClick }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.BoostSpeedHold,
            MouseTrigger = new PlayerMouseTrigger { Button = MouseButton.Right, Kind = PlayerInputTriggerKind.MouseHold }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.BoostSpeedRelease,
            MouseTrigger = new PlayerMouseTrigger { Button = MouseButton.Right, Kind = PlayerInputTriggerKind.MouseRelease }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.ExitFullscreenOrBack,
            MouseTrigger = new PlayerMouseTrigger { Button = MouseButton.XButton1, Kind = PlayerInputTriggerKind.MouseClick }
        });

        return profile;
    }
}
