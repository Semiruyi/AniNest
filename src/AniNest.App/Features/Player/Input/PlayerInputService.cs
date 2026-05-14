using System;
using System.Threading;
using System.Threading.Tasks;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Presentation;

namespace AniNest.Features.Player.Input;

public sealed class PlayerInputService : IPlayerInputService
{
    private static readonly Logger Log = AppLog.For<PlayerInputService>();

    private const int ClickDelayMs = 200;
    private const int HoldDurationMs = 500;

    private readonly ISettingsService _settings;
    private readonly IUiDispatcher _uiDispatcher;
    private PlayerInputProfile _profile;
    private CancellationTokenSource? _leftClickDelay;
    private IPlayerInputHost? _pendingLeftClickHost;
    private bool _skipNextLeftUp;
    private CancellationTokenSource? _rightHoldDelay;
    private IPlayerInputHost? _rightHoldHost;
    private bool _rightDown;
    private bool _rightHoldTriggered;

    public PlayerInputService(ISettingsService settings, IUiDispatcher uiDispatcher)
    {
        _settings = settings;
        _uiDispatcher = uiDispatcher;
        _profile = LoadProfile(settings.Load());
        Log.Info($"Initialized with {_profile.Bindings.Count} bindings");
        foreach (var b in _profile.Bindings)
        {
            if (b.IsEnabled)
                Log.Debug($"  Binding: Action={b.Action} Key={b.KeyTrigger?.Key} Mouse={b.MouseTrigger?.Kind}:{b.MouseTrigger?.Button}");
        }
    }

    public PlayerInputProfile CurrentProfile => _profile.Clone();

    public void ReloadProfile()
    {
        Log.Info("ReloadProfile called");
        ResetTransientState();
        _settings.Reload();
        _profile = LoadProfile(_settings.Load());
        Log.Info($"ReloadProfile done, {_profile.Bindings.Count} bindings loaded");
    }

    public void SaveProfile(PlayerInputProfile profile)
    {
        Log.Info($"SaveProfile called with {profile.Bindings.Count} bindings");
        foreach (var b in profile.Bindings)
            Log.Debug($"  Save input: Action={b.Action} Enabled={b.IsEnabled} Key={b.KeyTrigger?.Key} Mod={b.KeyTrigger?.Modifiers} Mouse={b.MouseTrigger?.Kind}:{b.MouseTrigger?.Button}");

        var normalized = NormalizeProfile(profile);
        Log.Info($"Normalized to {normalized.Bindings.Count} bindings");

        var settings = _settings.Load();
        settings.PlayerInput = normalized.Clone();
        _settings.Save();
        _profile = normalized;
        ResetTransientState();
        Log.Info("SaveProfile completed, _profile updated");
    }

    public bool TryHandleKeyDown(IPlayerInputHost host, PlayerInputKeyEvent inputEvent)
    {
        if (inputEvent.ShouldSkip)
        {
            Log.Debug($"KeyDown skipped (editable focus): Key={inputEvent.Key}");
            return false;
        }

        Log.Debug($"KeyDown: Key={inputEvent.Key} IsRepeat={inputEvent.IsRepeat} Modifiers={inputEvent.Modifiers}");

        foreach (var binding in _profile.Bindings)
        {
            if (!binding.IsEnabled || binding.KeyTrigger is null)
                continue;

            if (!binding.KeyTrigger.AllowRepeat && inputEvent.IsRepeat)
                continue;

            if (binding.KeyTrigger.Key != inputEvent.Key)
                continue;

            if (binding.KeyTrigger.Modifiers != inputEvent.Modifiers)
            {
                Log.Debug($"  Modifier mismatch for {binding.Action}: binding={binding.KeyTrigger.Modifiers} actual={inputEvent.Modifiers}");
                continue;
            }

            Log.Info($"  Key matched: Action={binding.Action} Key={inputEvent.Key}");
            if (host.TryHandleInput(binding.Action))
                return true;
        }

        Log.Debug("  No binding matched");
        return false;
    }

    public bool TryHandleMouseDown(IPlayerInputHost host, PlayerInputMouseButtonEvent inputEvent)
    {
        if (inputEvent.ShouldSkip)
            return false;

        if (inputEvent.Button == PlayerInputMouseButton.Left)
        {
            if (!inputEvent.IsInVideoSurface)
                return false;

            if (inputEvent.ClickCount > 1 || _leftClickDelay is not null)
            {
                CancelPendingLeftClick();
                _skipNextLeftUp = true;
                return TryExecuteMouseBinding(host, inputEvent.Button, PlayerInputTriggerKind.MouseDoubleClick, inputEvent.Modifiers);
            }

            return false;
        }

        if (inputEvent.Button == PlayerInputMouseButton.Right)
        {
            _rightDown = true;
            _rightHoldTriggered = false;
            _rightHoldHost = host;
            CancelScheduledAction(ref _rightHoldDelay);

            if (HasMouseBinding(inputEvent.Button, PlayerInputTriggerKind.MouseHold))
            {
                _rightHoldDelay = ScheduleDelayedUiAction(HoldDurationMs, () =>
                {
                    if (!_rightDown || _rightHoldHost is null)
                        return;

                    _rightHoldTriggered = TryExecuteMouseBinding(_rightHoldHost, inputEvent.Button, PlayerInputTriggerKind.MouseHold, inputEvent.Modifiers);
                });
            }

            return false;
        }

        return TryExecuteMouseBinding(host, inputEvent.Button, PlayerInputTriggerKind.MouseClick, inputEvent.Modifiers);
    }

    public bool TryHandleMouseUp(IPlayerInputHost host, PlayerInputMouseButtonEvent inputEvent)
    {
        if (inputEvent.ShouldSkip)
            return false;

        if (inputEvent.Button == PlayerInputMouseButton.Left)
        {
            if (!inputEvent.IsInVideoSurface)
                return false;

            if (_skipNextLeftUp)
            {
                _skipNextLeftUp = false;
                return false;
            }

            if (HasMouseBinding(inputEvent.Button, PlayerInputTriggerKind.MouseClick))
            {
                CancelPendingLeftClick();
                _pendingLeftClickHost = host;
                _leftClickDelay = ScheduleDelayedUiAction(ClickDelayMs, () =>
                {
                    if (_pendingLeftClickHost is null)
                        return;

                    TryExecuteMouseBinding(_pendingLeftClickHost, inputEvent.Button, PlayerInputTriggerKind.MouseClick, inputEvent.Modifiers);
                    CancelPendingLeftClick();
                });
            }

            return false;
        }

        if (inputEvent.Button == PlayerInputMouseButton.Right && _rightHoldTriggered)
        {
            _rightDown = false;
            CancelScheduledAction(ref _rightHoldDelay);
            _rightHoldTriggered = false;
            return TryExecuteMouseBinding(host, inputEvent.Button, PlayerInputTriggerKind.MouseRelease, inputEvent.Modifiers);
        }

        if (inputEvent.Button == PlayerInputMouseButton.Right)
        {
            _rightDown = false;
            CancelScheduledAction(ref _rightHoldDelay);
            _rightHoldHost = null;
        }

        return false;
    }

    public bool TryHandleMouseWheel(IPlayerInputHost host, PlayerInputMouseWheelEvent inputEvent)
    {
        if (inputEvent.ShouldSkip)
            return false;

        var kind = inputEvent.Delta > 0 ? PlayerInputTriggerKind.MouseWheelUp : PlayerInputTriggerKind.MouseWheelDown;
        foreach (var binding in _profile.Bindings)
        {
            if (!binding.IsEnabled || binding.MouseTrigger is null)
                continue;

            if (binding.MouseTrigger.Kind != kind)
                continue;

            if (binding.MouseTrigger.Modifiers != inputEvent.Modifiers)
                continue;

            if (host.TryHandleInput(binding.Action))
                return true;
        }

        return false;
    }

    private bool TryExecuteMouseBinding(
        IPlayerInputHost host,
        PlayerInputMouseButton button,
        PlayerInputTriggerKind kind,
        PlayerInputModifiers modifiers)
    {
        foreach (var binding in _profile.Bindings)
        {
            if (!binding.IsEnabled || binding.MouseTrigger is null)
                continue;

            if (binding.MouseTrigger.Button != button || binding.MouseTrigger.Kind != kind)
                continue;

            if (binding.MouseTrigger.Modifiers != modifiers)
                continue;

            if (host.TryHandleInput(binding.Action))
                return true;
        }

        return false;
    }

    private bool HasMouseBinding(PlayerInputMouseButton button, PlayerInputTriggerKind kind)
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
        CancelScheduledAction(ref _leftClickDelay);
        _pendingLeftClickHost = null;
    }

    private void ResetTransientState()
    {
        CancelPendingLeftClick();
        _skipNextLeftUp = false;
        CancelScheduledAction(ref _rightHoldDelay);
        _rightHoldHost = null;
        _rightDown = false;
        _rightHoldTriggered = false;
    }

    private CancellationTokenSource ScheduleDelayedUiAction(int ms, Action action)
    {
        var cts = new CancellationTokenSource();
        _ = RunDelayedUiActionAsync(TimeSpan.FromMilliseconds(ms), action, cts.Token);
        return cts;
    }

    private async Task RunDelayedUiActionAsync(TimeSpan delay, Action action, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
            return;

        _uiDispatcher.BeginInvoke(() =>
        {
            if (!cancellationToken.IsCancellationRequested)
                action();
        });
    }

    private static void CancelScheduledAction(ref CancellationTokenSource? cancellationTokenSource)
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
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
}
