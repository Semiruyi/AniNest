namespace AniNest.Features.Player.Input;

public static class PlayerInputDefaults
{
    public static PlayerInputProfile Create()
    {
        var profile = new PlayerInputProfile();
        var bindings = profile.Bindings;

        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.PlayPause,
            KeyTrigger = new PlayerKeyTrigger { Key = PlayerInputKey.Space, AllowRepeat = false }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.PlayPause,
            MouseTrigger = new PlayerMouseTrigger { Button = PlayerInputMouseButton.Left, Kind = PlayerInputTriggerKind.MouseClick }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.Stop,
            KeyTrigger = new PlayerKeyTrigger { Key = PlayerInputKey.S, AllowRepeat = false }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.SeekBackwardSmall,
            KeyTrigger = new PlayerKeyTrigger { Key = PlayerInputKey.Left }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.SeekForwardSmall,
            KeyTrigger = new PlayerKeyTrigger { Key = PlayerInputKey.Right }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.SeekBackwardLarge,
            KeyTrigger = new PlayerKeyTrigger { Key = PlayerInputKey.Left, Modifiers = PlayerInputModifiers.Control }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.SeekForwardLarge,
            KeyTrigger = new PlayerKeyTrigger { Key = PlayerInputKey.Right, Modifiers = PlayerInputModifiers.Control }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.Previous,
            KeyTrigger = new PlayerKeyTrigger { Key = PlayerInputKey.PageUp, AllowRepeat = false }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.Next,
            KeyTrigger = new PlayerKeyTrigger { Key = PlayerInputKey.PageDown, AllowRepeat = false }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.ToggleFullscreen,
            KeyTrigger = new PlayerKeyTrigger { Key = PlayerInputKey.Enter, AllowRepeat = false }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.ExitFullscreenOrBack,
            KeyTrigger = new PlayerKeyTrigger { Key = PlayerInputKey.Escape, AllowRepeat = false }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.TogglePlaylist,
            KeyTrigger = new PlayerKeyTrigger { Key = PlayerInputKey.L, AllowRepeat = false }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.ToggleFullscreen,
            MouseTrigger = new PlayerMouseTrigger { Button = PlayerInputMouseButton.Left, Kind = PlayerInputTriggerKind.MouseDoubleClick }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.BoostSpeedHold,
            MouseTrigger = new PlayerMouseTrigger { Button = PlayerInputMouseButton.Right, Kind = PlayerInputTriggerKind.MouseHold }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.BoostSpeedRelease,
            MouseTrigger = new PlayerMouseTrigger { Button = PlayerInputMouseButton.Right, Kind = PlayerInputTriggerKind.MouseRelease }
        });
        bindings.Add(new PlayerInputBinding
        {
            Action = PlayerInputAction.ExitFullscreenOrBack,
            MouseTrigger = new PlayerMouseTrigger { Button = PlayerInputMouseButton.XButton1, Kind = PlayerInputTriggerKind.MouseClick }
        });

        return profile;
    }
}
