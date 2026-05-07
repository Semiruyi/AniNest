using System.Windows.Input;

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

    public bool TryCaptureKey(KeyEventArgs args, out PlayerInputBinding? binding)
    {
        binding = null;
        if (!IsCapturing)
            return false;

        var key = args.Key == Key.System ? args.SystemKey : args.Key;
        if (IsModifierOnlyKey(key))
            return false;

        binding = new PlayerInputBinding
        {
            Action = PlayerInputAction.PlayPause,
            KeyTrigger = new PlayerKeyTrigger
            {
                Key = key,
                Modifiers = Keyboard.Modifiers,
                AllowRepeat = false
            }
        };
        IsCapturing = false;
        return true;
    }

    public bool TryCaptureMouseDown(MouseButtonEventArgs args, out PlayerInputBinding? binding)
    {
        binding = null;
        if (!IsCapturing)
            return false;

        binding = new PlayerInputBinding
        {
            Action = PlayerInputAction.PlayPause,
            MouseTrigger = new PlayerMouseTrigger
            {
                Button = args.ChangedButton,
                Modifiers = Keyboard.Modifiers,
                Kind = args.ClickCount > 1 ? PlayerInputTriggerKind.MouseDoubleClick : PlayerInputTriggerKind.MouseClick
            }
        };
        IsCapturing = false;
        return true;
    }

    public bool TryCaptureMouseWheel(MouseWheelEventArgs args, out PlayerInputBinding? binding)
    {
        binding = null;
        if (!IsCapturing)
            return false;

        binding = new PlayerInputBinding
        {
            Action = PlayerInputAction.PlayPause,
            MouseTrigger = new PlayerMouseTrigger
            {
                Modifiers = Keyboard.Modifiers,
                Kind = args.Delta >= 0 ? PlayerInputTriggerKind.MouseWheelUp : PlayerInputTriggerKind.MouseWheelDown
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

    private static bool IsModifierOnlyKey(Key key)
        => key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin;
}
