using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace LocalPlayer.Features.Player.Input;

public sealed class PlayerInputService : IPlayerInputService
{
    private readonly PlayerInputProfile _profile = PlayerInputDefaults.Create();
    private bool _rightHoldTriggered;

    public bool TryHandlePreviewKeyDown(IPlayerInputHost host, KeyEventArgs args)
    {
        if (args.Handled || ShouldSkipKeyboard(args.OriginalSource as DependencyObject))
            return false;

        foreach (var binding in _profile.Bindings)
        {
            if (!binding.IsEnabled || binding.KeyTrigger is null)
                continue;

            if (!binding.KeyTrigger.AllowRepeat && args.IsRepeat)
                continue;

            if (binding.KeyTrigger.Key != args.Key)
                continue;

            if (binding.KeyTrigger.Modifiers != Keyboard.Modifiers)
                continue;

            if (host.TryHandleInput(binding.Action))
            {
                args.Handled = true;
                return true;
            }
        }

        return false;
    }

    public bool TryHandlePreviewMouseDown(IPlayerInputHost host, MouseButtonEventArgs args)
    {
        if (args.Handled || ShouldSkipMouse(args.OriginalSource as DependencyObject))
            return false;

        if (args.ChangedButton == MouseButton.Right)
            _rightHoldTriggered = false;

        var kind = args.ClickCount > 1 ? PlayerInputTriggerKind.MouseDoubleClick : PlayerInputTriggerKind.MouseClick;
        if (TryExecuteMouseBinding(host, args.ChangedButton, kind, args))
            return true;

        if (args.ChangedButton == MouseButton.Right &&
            TryExecuteMouseBinding(host, MouseButton.Right, PlayerInputTriggerKind.MouseHold, args))
        {
            _rightHoldTriggered = true;
            return true;
        }

        return false;
    }

    public bool TryHandlePreviewMouseUp(IPlayerInputHost host, MouseButtonEventArgs args)
    {
        if (args.Handled || ShouldSkipMouse(args.OriginalSource as DependencyObject))
            return false;

        if (args.ChangedButton == MouseButton.Right && _rightHoldTriggered)
        {
            _rightHoldTriggered = false;
            return TryExecuteMouseBinding(host, MouseButton.Right, PlayerInputTriggerKind.MouseRelease, args);
        }

        return false;
    }

    public bool TryHandlePreviewMouseWheel(IPlayerInputHost host, MouseWheelEventArgs args)
    {
        if (args.Handled || ShouldSkipMouse(args.OriginalSource as DependencyObject))
            return false;

        var kind = args.Delta > 0 ? PlayerInputTriggerKind.MouseWheelUp : PlayerInputTriggerKind.MouseWheelDown;
        foreach (var binding in _profile.Bindings)
        {
            if (!binding.IsEnabled || binding.MouseTrigger is null)
                continue;

            if (binding.MouseTrigger.Kind != kind)
                continue;

            if (binding.MouseTrigger.Modifiers != Keyboard.Modifiers)
                continue;

            if (host.TryHandleInput(binding.Action))
            {
                args.Handled = true;
                return true;
            }
        }

        return false;
    }

    private bool TryExecuteMouseBinding(IPlayerInputHost host, MouseButton button, PlayerInputTriggerKind kind, MouseButtonEventArgs args)
    {
        foreach (var binding in _profile.Bindings)
        {
            if (!binding.IsEnabled || binding.MouseTrigger is null)
                continue;

            if (binding.MouseTrigger.Button != button || binding.MouseTrigger.Kind != kind)
                continue;

            if (binding.MouseTrigger.Modifiers != Keyboard.Modifiers)
                continue;

            if (host.TryHandleInput(binding.Action))
            {
                args.Handled = true;
                return true;
            }
        }

        return false;
    }

    private static bool ShouldSkipKeyboard(DependencyObject? source)
        => HasAncestor<TextBoxBase>(source)
            || HasAncestor<PasswordBox>(source)
            || HasAncestor<ComboBox>(source)
            || HasAncestor<ButtonBase>(source);

    private static bool ShouldSkipMouse(DependencyObject? source)
        => HasAncestor<ButtonBase>(source)
            || HasAncestor<Thumb>(source)
            || HasAncestor<TextBoxBase>(source)
            || HasAncestor<PasswordBox>(source)
            || HasAncestor<ComboBox>(source)
            || HasAncestor<ListBoxItem>(source);

    private static bool HasAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        var current = source;
        while (current != null)
        {
            if (current is T)
                return true;

            current = current switch
            {
                Visual visual => System.Windows.Media.VisualTreeHelper.GetParent(visual),
                System.Windows.Media.Media3D.Visual3D visual3D => System.Windows.Media.VisualTreeHelper.GetParent(visual3D),
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return false;
    }
}
