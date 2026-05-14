namespace AniNest.Features.Player.Input;

public sealed class PlayerInputCaptureSession
{
    public bool IsCapturing { get; private set; }

    public void Begin()
    {
        IsCapturing = true;
    }

    public void Cancel()
    {
        IsCapturing = false;
    }

    public bool TryCaptureKey(PlayerInputKeyEvent inputEvent, out PlayerInputBinding? binding)
    {
        binding = null;
        if (!IsCapturing)
            return false;

        if (IsModifierOnlyKey(inputEvent.Key) || inputEvent.Key == PlayerInputKey.Unknown)
            return false;

        binding = new PlayerInputBinding
        {
            Action = PlayerInputAction.PlayPause,
            KeyTrigger = new PlayerKeyTrigger
            {
                Key = inputEvent.Key,
                Modifiers = inputEvent.Modifiers,
                AllowRepeat = false
            }
        };
        IsCapturing = false;
        return true;
    }

    public bool TryCaptureMouseDown(PlayerInputMouseButtonEvent inputEvent, out PlayerInputBinding? binding)
    {
        binding = null;
        if (!IsCapturing)
            return false;

        binding = new PlayerInputBinding
        {
            Action = PlayerInputAction.PlayPause,
            MouseTrigger = new PlayerMouseTrigger
            {
                Button = inputEvent.Button,
                Modifiers = inputEvent.Modifiers,
                Kind = inputEvent.ClickCount > 1 ? PlayerInputTriggerKind.MouseDoubleClick : PlayerInputTriggerKind.MouseClick
            }
        };
        IsCapturing = false;
        return true;
    }

    public bool TryCaptureMouseWheel(PlayerInputMouseWheelEvent inputEvent, out PlayerInputBinding? binding)
    {
        binding = null;
        if (!IsCapturing)
            return false;

        binding = new PlayerInputBinding
        {
            Action = PlayerInputAction.PlayPause,
            MouseTrigger = new PlayerMouseTrigger
            {
                Modifiers = inputEvent.Modifiers,
                Kind = inputEvent.Delta >= 0 ? PlayerInputTriggerKind.MouseWheelUp : PlayerInputTriggerKind.MouseWheelDown
            }
        };
        IsCapturing = false;
        return true;
    }

    public static PlayerInputBinding WithAction(PlayerInputBinding template, PlayerInputAction action) => new()
    {
        Action = action,
        KeyTrigger = template.KeyTrigger?.Clone(),
        MouseTrigger = template.MouseTrigger?.Clone(),
        IsEnabled = template.IsEnabled
    };

    private static bool IsModifierOnlyKey(PlayerInputKey key)
        => key is PlayerInputKey.LeftCtrl or PlayerInputKey.RightCtrl
            or PlayerInputKey.LeftAlt or PlayerInputKey.RightAlt
            or PlayerInputKey.LeftShift or PlayerInputKey.RightShift
            or PlayerInputKey.LWin or PlayerInputKey.RWin;
}
