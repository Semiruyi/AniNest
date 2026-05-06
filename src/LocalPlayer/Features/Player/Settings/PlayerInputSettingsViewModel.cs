using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalPlayer.Features.Player.Input;
using LocalPlayer.Infrastructure.Localization;
using LocalPlayer.Infrastructure.Logging;

namespace LocalPlayer.Features.Player.Settings;

public partial class PlayerInputSettingsViewModel : ObservableObject
{
    private static readonly Logger Log = AppLog.For<PlayerInputSettingsViewModel>();

    private readonly IPlayerInputService _inputService;
    private readonly ILocalizationService _localization;
    private readonly PlayerInputCaptureSession _captureSession = new();
    private readonly PropertyChangedEventHandler _localizationChangedHandler;
    private PlayerInputProfile _profile;
    private int _capturingIndex = -1;

    public ObservableCollection<PlayerInputBindingItemViewModel> Items { get; } = new();

    public PlayerInputSettingsViewModel(
        IPlayerInputService inputService,
        ILocalizationService localization)
    {
        _inputService = inputService;
        _localization = localization;
        _profile = _inputService.CurrentProfile;
        _localizationChangedHandler = (_, args) =>
        {
            if (args.PropertyName is nameof(ILocalizationService.CurrentLanguage) or "Item[]")
                RefreshDisplay();
        };
        _localization.PropertyChanged += _localizationChangedHandler;
        RebuildItems();
    }

    public string Title => _localization["Settings.PlayerInput"];
    public string ResetButtonText => _localization["Settings.PlayerInput.Reset"];
    public string CaptureButtonText => _localization["Settings.PlayerInput.Record"];
    public string CapturingButtonText => _localization["Settings.PlayerInput.Capturing"];
    public string ClearButtonText => _localization["Settings.PlayerInput.Clear"];
    public string HintText => _localization["Settings.PlayerInput.Hint"];

    public bool TryCaptureKey(KeyEventArgs args)
    {
        Log.Debug($"TryCaptureKey: Key={args.Key} SystemKey={args.SystemKey} CapturingIndex={_capturingIndex} IsCapturing={_captureSession.IsCapturing}");
        if (_capturingIndex < 0 || !_captureSession.TryCaptureKey(args, out var template) || template is null)
            return false;

        Log.Info($"Key captured: Key={template.KeyTrigger?.Key} Modifiers={template.KeyTrigger?.Modifiers}");
        ApplyCapturedBinding(template);
        return true;
    }

    public bool TryCaptureMouseDown(MouseButtonEventArgs args)
    {
        if (_capturingIndex < 0 || !_captureSession.TryCaptureMouseDown(args, out var template) || template is null)
            return false;

        ApplyCapturedBinding(template);
        return true;
    }

    public bool TryCaptureMouseWheel(MouseWheelEventArgs args)
    {
        if (_capturingIndex < 0 || !_captureSession.TryCaptureMouseWheel(args, out var template) || template is null)
            return false;

        ApplyCapturedBinding(template);
        return true;
    }

    public void CancelCapture()
    {
        _captureSession.Cancel();
        UpdateCapturingState(-1);
    }

    public void RefreshFromService()
    {
        Log.Info("RefreshFromService called");
        _profile = _inputService.CurrentProfile;
        Log.Info($"RefreshFromService: loaded {_profile.Bindings.Count} bindings from service");
        RebuildItems();
    }

    [RelayCommand]
    private void BeginCapture(int index)
    {
        if (index < 0 || index >= _profile.Bindings.Count)
        {
            Log.Warning($"BeginCapture: invalid index {index}, bindings count={_profile.Bindings.Count}");
            return;
        }

        Log.Info($"BeginCapture: index={index} Action={_profile.Bindings[index].Action}");
        _captureSession.Begin();
        UpdateCapturingState(index);
    }

    [RelayCommand]
    private void ClearBinding(int index)
    {
        if (index < 0 || index >= _profile.Bindings.Count)
            return;

        var existing = _profile.Bindings[index];
        _profile.Bindings[index] = new PlayerInputBinding
        {
            Action = existing.Action,
            KeyTrigger = existing.KeyTrigger?.Clone(),
            MouseTrigger = existing.MouseTrigger?.Clone(),
            IsEnabled = false
        };
        SaveAndRefresh();
    }

    [RelayCommand]
    private void Reset()
    {
        _profile = PlayerInputDefaults.Create();
        SaveAndRefresh();
    }

    private void ApplyCapturedBinding(PlayerInputBinding template)
    {
        if (_capturingIndex < 0 || _capturingIndex >= _profile.Bindings.Count)
        {
            Log.Warning($"ApplyCapturedBinding: invalid index {_capturingIndex}, canceling");
            CancelCapture();
            return;
        }

        var existing = _profile.Bindings[_capturingIndex];
        var replacement = PlayerInputCaptureSession.WithAction(template, existing.Action);
        Log.Info($"ApplyCapturedBinding: index={_capturingIndex} Action={replacement.Action} Key={replacement.KeyTrigger?.Key} Mod={replacement.KeyTrigger?.Modifiers} Mouse={replacement.MouseTrigger?.Kind}:{replacement.MouseTrigger?.Button}");

        var conflicts = PlayerInputConflictDetector.FindConflicts(_profile, replacement, _capturingIndex);
        if (conflicts.Count > 0)
        {
            Log.Info($"  Conflicts found: {conflicts.Count}");
            foreach (var c in conflicts)
                Log.Debug($"  Conflict with index={c.ExistingIndex} Action={c.ExistingBinding.Action}");

            var conflictNames = string.Join(
                " / ",
                conflicts.Select(c => PlayerInputFormatter.FormatAction(_localization, c.ExistingBinding.Action)));

            var result = MessageBox.Show(
                string.Format(_localization["Settings.PlayerInput.ConflictMessage"], conflictNames),
                _localization["Settings.PlayerInput.ConflictTitle"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                Log.Info("  User declined conflict resolution, canceling");
                CancelCapture();
                return;
            }

            foreach (var conflict in conflicts)
            {
                var conflictBinding = _profile.Bindings[conflict.ExistingIndex];
                _profile.Bindings[conflict.ExistingIndex] = new PlayerInputBinding
                {
                    Action = conflictBinding.Action,
                    KeyTrigger = conflictBinding.KeyTrigger?.Clone(),
                    MouseTrigger = conflictBinding.MouseTrigger?.Clone(),
                    IsEnabled = false
                };
                Log.Debug($"  Disabled conflicting binding at index={conflict.ExistingIndex}");
            }
        }

        _profile.Bindings[_capturingIndex] = replacement;
        SaveAndRefresh();
    }

    private void SaveAndRefresh()
    {
        Log.Info("SaveAndRefresh called");
        CancelCapture();
        _inputService.SaveProfile(_profile);
        _profile = _inputService.CurrentProfile;
        Log.Info($"SaveAndRefresh done, reloaded profile has {_profile.Bindings.Count} bindings");
        foreach (var b in _profile.Bindings)
            Log.Debug($"  After save: Action={b.Action} Enabled={b.IsEnabled} Key={b.KeyTrigger?.Key} Mouse={b.MouseTrigger?.Kind}:{b.MouseTrigger?.Button}");
        RebuildItems();
    }

    private void RebuildItems()
    {
        Items.Clear();
        for (int i = 0; i < _profile.Bindings.Count; i++)
        {
            var binding = _profile.Bindings[i];
            Items.Add(new PlayerInputBindingItemViewModel
            {
                Index = i,
                Action = binding.Action,
                ActionDisplayName = PlayerInputFormatter.FormatAction(_localization, binding.Action),
                BindingDisplay = PlayerInputFormatter.FormatBinding(_localization, binding),
                IsCapturing = i == _capturingIndex
            });
        }

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ResetButtonText));
        OnPropertyChanged(nameof(CaptureButtonText));
        OnPropertyChanged(nameof(CapturingButtonText));
        OnPropertyChanged(nameof(ClearButtonText));
        OnPropertyChanged(nameof(HintText));
    }

    private void RefreshDisplay()
    {
        for (int i = 0; i < Items.Count && i < _profile.Bindings.Count; i++)
        {
            Items[i].ActionDisplayName = PlayerInputFormatter.FormatAction(_localization, _profile.Bindings[i].Action);
            Items[i].BindingDisplay = PlayerInputFormatter.FormatBinding(_localization, _profile.Bindings[i]);
        }

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ResetButtonText));
        OnPropertyChanged(nameof(CaptureButtonText));
        OnPropertyChanged(nameof(CapturingButtonText));
        OnPropertyChanged(nameof(ClearButtonText));
        OnPropertyChanged(nameof(HintText));
    }

    private void UpdateCapturingState(int index)
    {
        _capturingIndex = index;
        for (int i = 0; i < Items.Count; i++)
            Items[i].IsCapturing = i == index;
    }
}
