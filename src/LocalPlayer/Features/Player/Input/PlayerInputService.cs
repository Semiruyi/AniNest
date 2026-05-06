using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LocalPlayer.Infrastructure.Persistence;

namespace LocalPlayer.Features.Player.Input;

public sealed class PlayerInputService : IPlayerInputService
{
    private const int ClickDelayMs = 200;
    private const int HoldDurationMs = 500;

    private readonly ISettingsService _settings;
    private PlayerInputProfile _profile;
    private DispatcherTimer? _leftClickTimer;
    private IPlayerInputHost? _pendingLeftClickHost;
    private bool _skipNextLeftUp;
    private DispatcherTimer? _rightHoldTimer;
    private IPlayerInputHost? _rightHoldHost;
    private bool _rightDown;
    private bool _rightHoldTriggered;

    public PlayerInputService(ISettingsService settings)
    {
        _settings = settings;
        _profile = LoadProfile(settings.Load());
    }

    public PlayerInputProfile CurrentProfile => _profile.Clone();

    public void ReloadProfile()
    {
        ResetTransientState();
        _settings.Reload();
        _profile = LoadProfile(_settings.Load());
    }

    public void SaveProfile(PlayerInputProfile profile)
    {
        var normalized = NormalizeProfile(profile);
        var settings = _settings.Load();
        settings.PlayerInput = normalized.Clone();
        _settings.Save();
        _profile = normalized;
        ResetTransientState();
    }

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

        if (args.ChangedButton == MouseButton.Left)
        {
            if (args.ClickCount > 1 || _leftClickTimer != null)
            {
                CancelPendingLeftClick();
                _skipNextLeftUp = true;
                return TryExecuteMouseBinding(host, MouseButton.Left, PlayerInputTriggerKind.MouseDoubleClick, args);
            }

            return false;
        }

        if (args.ChangedButton == MouseButton.Right)
        {
            _rightDown = true;
            _rightHoldTriggered = false;
            _rightHoldHost = host;
            _rightHoldTimer?.Stop();

            if (HasMouseBinding(MouseButton.Right, PlayerInputTriggerKind.MouseHold))
            {
                _rightHoldTimer = NewTimer(HoldDurationMs, () =>
                {
                    if (!_rightDown || _rightHoldHost is null)
                        return;

                    _rightHoldTriggered = TryExecuteMouseBinding(_rightHoldHost, MouseButton.Right, PlayerInputTriggerKind.MouseHold);
                });
                _rightHoldTimer.Start();
            }

            return false;
        }

        return TryExecuteMouseBinding(host, args.ChangedButton, PlayerInputTriggerKind.MouseClick, args);
    }

    public bool TryHandlePreviewMouseUp(IPlayerInputHost host, MouseButtonEventArgs args)
    {
        if (args.Handled || ShouldSkipMouse(args.OriginalSource as DependencyObject))
            return false;

        if (args.ChangedButton == MouseButton.Left)
        {
            if (_skipNextLeftUp)
            {
                _skipNextLeftUp = false;
                return false;
            }

            if (HasMouseBinding(MouseButton.Left, PlayerInputTriggerKind.MouseClick))
            {
                CancelPendingLeftClick();
                _pendingLeftClickHost = host;
                _leftClickTimer = NewTimer(ClickDelayMs, () =>
                {
                    if (_pendingLeftClickHost is null)
                        return;

                    TryExecuteMouseBinding(_pendingLeftClickHost, MouseButton.Left, PlayerInputTriggerKind.MouseClick);
                    CancelPendingLeftClick();
                });
                _leftClickTimer.Start();
            }

            return false;
        }

        if (args.ChangedButton == MouseButton.Right && _rightHoldTriggered)
        {
            _rightDown = false;
            _rightHoldTimer?.Stop();
            _rightHoldTriggered = false;
            return TryExecuteMouseBinding(host, MouseButton.Right, PlayerInputTriggerKind.MouseRelease, args);
        }

        if (args.ChangedButton == MouseButton.Right)
        {
            _rightDown = false;
            _rightHoldTimer?.Stop();
            _rightHoldHost = null;
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

    private bool TryExecuteMouseBinding(IPlayerInputHost host, MouseButton button, PlayerInputTriggerKind kind)
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
                return true;
        }

        return false;
    }

    private bool TryExecuteMouseBinding(IPlayerInputHost host, MouseButton button, PlayerInputTriggerKind kind, MouseButtonEventArgs args)
    {
        var handled = TryExecuteMouseBinding(host, button, kind);
        if (handled)
            args.Handled = true;
        return handled;
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

    private bool HasMouseBinding(MouseButton button, PlayerInputTriggerKind kind)
    {
        foreach (var binding in _profile.Bindings)
        {
            if (!binding.IsEnabled || binding.MouseTrigger is null)
                continue;

            if (binding.MouseTrigger.Button == button && binding.MouseTrigger.Kind == kind)
                return true;
        }

        return false;
    }

    private void CancelPendingLeftClick()
    {
        _leftClickTimer?.Stop();
        _leftClickTimer = null;
        _pendingLeftClickHost = null;
    }

    private void ResetTransientState()
    {
        CancelPendingLeftClick();
        _skipNextLeftUp = false;
        _rightHoldTimer?.Stop();
        _rightHoldTimer = null;
        _rightHoldHost = null;
        _rightDown = false;
        _rightHoldTriggered = false;
    }

    private static PlayerInputProfile LoadProfile(AppSettings settings)
    {
        if (settings.PlayerInput is { HasBindings: true })
            return NormalizeProfile(settings.PlayerInput);

        return PlayerInputDefaults.Create();
    }

    private static PlayerInputProfile NormalizeProfile(PlayerInputProfile? profile)
    {
        if (profile is null || !profile.HasBindings)
            return PlayerInputDefaults.Create();

        var normalized = new PlayerInputProfile();
        foreach (var binding in profile.Bindings)
        {
            if (binding.KeyTrigger is null && binding.MouseTrigger is null)
                continue;

            normalized.Bindings.Add(binding.Clone());
        }

        return normalized.HasBindings ? normalized : PlayerInputDefaults.Create();
    }

    private static DispatcherTimer NewTimer(int ms, Action action)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        return timer;
    }

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
