using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AniNest.Presentation.Primitives;

namespace AniNest.Features.Player.Input;

public static class WpfPlayerInputEventAdapter
{
    public static PlayerInputKeyEvent CreateKeyEvent(KeyEventArgs args)
    {
        var key = args.Key == Key.System ? args.SystemKey : args.Key;
        return new PlayerInputKeyEvent
        {
            Key = WpfPlayerInputMapper.ToPlayerKey(key),
            Modifiers = WpfPlayerInputMapper.ToPlayerModifiers(Keyboard.Modifiers),
            IsRepeat = args.IsRepeat,
            ShouldSkip = ShouldSkipKeyboard(args.OriginalSource as DependencyObject)
        };
    }

    public static PlayerInputMouseButtonEvent CreateMouseButtonEvent(MouseButtonEventArgs args)
    {
        var source = args.OriginalSource as DependencyObject;
        return new PlayerInputMouseButtonEvent
        {
            Button = WpfPlayerInputMapper.ToPlayerMouseButton(args.ChangedButton),
            Modifiers = WpfPlayerInputMapper.ToPlayerModifiers(Keyboard.Modifiers),
            ClickCount = args.ClickCount,
            ShouldSkip = ShouldSkipMouse(source),
            IsInVideoSurface = source != null && IsInsideVideoSurface(source)
        };
    }

    public static PlayerInputMouseWheelEvent CreateMouseWheelEvent(MouseWheelEventArgs args)
    {
        return new PlayerInputMouseWheelEvent
        {
            Delta = args.Delta,
            Modifiers = WpfPlayerInputMapper.ToPlayerModifiers(Keyboard.Modifiers),
            ShouldSkip = ShouldSkipMouse(args.OriginalSource as DependencyObject)
        };
    }

    private static bool ShouldSkipKeyboard(DependencyObject? source)
        => HasAncestor<TextBoxBase>(source)
            || HasAncestor<PasswordBox>(source)
            || HasAncestor<ComboBox>(source)
            || HasAncestor<ButtonBase>(source);

    private static bool ShouldSkipMouse(DependencyObject? source)
        => HasAncestor<ButtonBase>(source)
            || HasAncestor<Thumb>(source)
            || HasAncestor<SeekBar>(source)
            || HasAncestor<TextBoxBase>(source)
            || HasAncestor<PasswordBox>(source)
            || HasAncestor<ComboBox>(source)
            || HasAncestor<ListBoxItem>(source);

    private static bool IsInsideVideoSurface(DependencyObject source)
        => HasAncestorNamed(source, "VideoContainer");

    private static bool HasAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        var current = source;
        while (current != null)
        {
            if (current is T)
                return true;

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                System.Windows.Media.Media3D.Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return false;
    }

    private static bool HasAncestorNamed(DependencyObject? source, string name)
    {
        var current = source;
        while (current != null)
        {
            if (current is FrameworkElement frameworkElement && frameworkElement.Name == name)
                return true;

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                System.Windows.Media.Media3D.Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return false;
    }
}
