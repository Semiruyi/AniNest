using System.Collections.Generic;
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

        if (trigger.Modifiers.HasFlag(PlayerInputModifiers.Control))
            parts.Add("Ctrl");
        if (trigger.Modifiers.HasFlag(PlayerInputModifiers.Shift))
            parts.Add("Shift");
        if (trigger.Modifiers.HasFlag(PlayerInputModifiers.Alt))
            parts.Add("Alt");
        if (trigger.Modifiers.HasFlag(PlayerInputModifiers.Windows))
            parts.Add("Win");

        parts.Add(FormatKey(trigger.Key));
        return string.Join(" + ", parts);
    }

    public static string FormatMouseTrigger(ILocalizationService localization, PlayerMouseTrigger trigger)
    {
        var parts = new List<string>(4);

        if (trigger.Modifiers.HasFlag(PlayerInputModifiers.Control))
            parts.Add("Ctrl");
        if (trigger.Modifiers.HasFlag(PlayerInputModifiers.Shift))
            parts.Add("Shift");
        if (trigger.Modifiers.HasFlag(PlayerInputModifiers.Alt))
            parts.Add("Alt");
        if (trigger.Modifiers.HasFlag(PlayerInputModifiers.Windows))
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

    private static string FormatMouseButton(ILocalizationService localization, PlayerInputMouseButton? button) => button switch
    {
        PlayerInputMouseButton.Left => localization["Player.Input.MouseLeft"],
        PlayerInputMouseButton.Right => localization["Player.Input.MouseRight"],
        PlayerInputMouseButton.Middle => localization["Player.Input.MouseMiddle"],
        PlayerInputMouseButton.XButton1 => localization["Player.Input.MouseX1"],
        PlayerInputMouseButton.XButton2 => localization["Player.Input.MouseX2"],
        _ => localization["Player.Input.Mouse"]
    };

    private static string FormatKey(PlayerInputKey key) => key switch
    {
        PlayerInputKey.Space => "Space",
        PlayerInputKey.Enter => "Enter",
        PlayerInputKey.PageUp => "PageUp",
        PlayerInputKey.PageDown => "PageDown",
        PlayerInputKey.Escape => "Esc",
        PlayerInputKey.Left => "Left",
        PlayerInputKey.Right => "Right",
        PlayerInputKey.Up => "Up",
        PlayerInputKey.Down => "Down",
        PlayerInputKey.OemPlus => "+",
        PlayerInputKey.OemMinus => "-",
        _ => key.ToString()
    };
}
