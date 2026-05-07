using System.Collections.Generic;
using System.Windows.Input;
using AniNest.Infrastructure.Localization;

namespace AniNest.Features.Player.Input;

public static class PlayerInputFormatter
{
    public static string FormatAction(ILocalizationService localization, PlayerInputAction action)
        => PlayerInputDisplayNames.GetActionDisplayName(localization, action);

    public static string FormatBinding(ILocalizationService localization, PlayerInputBinding binding)
    {
        if (!binding.IsEnabled)
            return localization["Player.Input.Unassigned"];

        if (binding.KeyTrigger is not null)
            return FormatKeyTrigger(binding.KeyTrigger);

        if (binding.MouseTrigger is not null)
            return FormatMouseTrigger(localization, binding.MouseTrigger);

        return localization["Player.Input.Unassigned"];
    }

    public static string FormatKeyTrigger(PlayerKeyTrigger trigger)
    {
        var parts = new List<string>(4);

        if (trigger.Modifiers.HasFlag(ModifierKeys.Control))
            parts.Add("Ctrl");
        if (trigger.Modifiers.HasFlag(ModifierKeys.Shift))
            parts.Add("Shift");
        if (trigger.Modifiers.HasFlag(ModifierKeys.Alt))
            parts.Add("Alt");
        if (trigger.Modifiers.HasFlag(ModifierKeys.Windows))
            parts.Add("Win");

        parts.Add(FormatKey(trigger.Key));
        return string.Join(" + ", parts);
    }

    public static string FormatMouseTrigger(ILocalizationService localization, PlayerMouseTrigger trigger)
    {
        var parts = new List<string>(4);

        if (trigger.Modifiers.HasFlag(ModifierKeys.Control))
            parts.Add("Ctrl");
        if (trigger.Modifiers.HasFlag(ModifierKeys.Shift))
            parts.Add("Shift");
        if (trigger.Modifiers.HasFlag(ModifierKeys.Alt))
            parts.Add("Alt");
        if (trigger.Modifiers.HasFlag(ModifierKeys.Windows))
            parts.Add("Win");

        parts.Add(FormatMouseKind(localization, trigger));
        return string.Join(" + ", parts);
    }

    private static string FormatMouseKind(ILocalizationService localization, PlayerMouseTrigger trigger)
    {
        return trigger.Kind switch
        {
            PlayerInputTriggerKind.MouseWheelUp => localization["Player.Input.MouseWheelUp"],
            PlayerInputTriggerKind.MouseWheelDown => localization["Player.Input.MouseWheelDown"],
            PlayerInputTriggerKind.MouseDoubleClick => string.Format(localization["Player.Input.MouseDoubleClick"], FormatMouseButton(localization, trigger.Button)),
            PlayerInputTriggerKind.MouseHold => string.Format(localization["Player.Input.MouseHold"], FormatMouseButton(localization, trigger.Button)),
            PlayerInputTriggerKind.MouseRelease => string.Format(localization["Player.Input.MouseRelease"], FormatMouseButton(localization, trigger.Button)),
            _ => FormatMouseButton(localization, trigger.Button)
        };
    }

    private static string FormatMouseButton(ILocalizationService localization, MouseButton? button) => button switch
    {
        MouseButton.Left => localization["Player.Input.MouseLeft"],
        MouseButton.Right => localization["Player.Input.MouseRight"],
        MouseButton.Middle => localization["Player.Input.MouseMiddle"],
        MouseButton.XButton1 => localization["Player.Input.MouseX1"],
        MouseButton.XButton2 => localization["Player.Input.MouseX2"],
        _ => localization["Player.Input.Mouse"]
    };

    private static string FormatKey(Key key) => key switch
    {
        Key.Space => "Space",
        Key.Return => "Enter",
        Key.Prior => "PageUp",
        Key.Next => "PageDown",
        Key.Escape => "Esc",
        Key.Left => "Left",
        Key.Right => "Right",
        Key.Up => "Up",
        Key.Down => "Down",
        Key.OemPlus => "+",
        Key.OemMinus => "-",
        _ => key.ToString()
    };
}
