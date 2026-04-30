using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalPlayer.Converters;
using LocalPlayer.Model;

namespace LocalPlayer.ViewModel;

public partial class KeyBindingsViewModel : ObservableObject
{
    public static void Log(string message) => AppLog.Info(nameof(KeyBindingsViewModel), message);

    private readonly PlayerInputHandler _inputHandler;

    public ObservableCollection<BindingItem> Items { get; } = new();

    public KeyBindingsViewModel(PlayerInputHandler inputHandler)
    {
        _inputHandler = inputHandler;
        LoadBindings();
    }

    private void LoadBindings()
    {
        var current = _inputHandler.GetCurrentBindings();
        var defaults = PlayerInputHandler.GetDefaultBindings();
        Items.Clear();
        foreach (var def in defaults)
        {
            var key = current.TryGetValue(def.ActionName, out var k) ? k : (Key)def.DefaultKey;
            Items.Add(new BindingItem
            {
                ActionName = def.ActionName,
                DisplayName = def.DisplayName,
                CurrentKey = key
            });
        }
    }

    public void StartWaiting(BindingItem item)
    {
        // Called from View code-behind to enter "waiting for key" mode
        item.IsWaiting = true;
    }

    public void CancelWaiting(BindingItem? item)
    {
        if (item != null)
            item.IsWaiting = false;
    }

    public bool TrySetBinding(BindingItem targetItem, Key newKey)
    {
        if (newKey == Key.Escape || newKey == Key.None)
            return false;

        var conflict = Items.FirstOrDefault(i => i != targetItem && i.CurrentKey == newKey);
        if (conflict != null)
        {
            var result = MessageBox.Show(
                $"按键 \"{conflict.CurrentKeyDisplay}\" 已绑定到 \"{conflict.DisplayName}\"。\n\n是否替换为该操作？",
                "按键冲突",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.OK)
                return false;

            conflict.CurrentKey = Key.None;
            _inputHandler.SetBinding(conflict.ActionName, Key.None);
        }

        targetItem.CurrentKey = newKey;
        _inputHandler.SetBinding(targetItem.ActionName, newKey);
        return true;
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        var defaults = PlayerInputHandler.GetDefaultBindings();
        foreach (var def in defaults)
            _inputHandler.SetBinding(def.ActionName, (Key)def.DefaultKey);
        LoadBindings();
    }
}

public class BindingItem : ObservableObject
{
    private static readonly KeyDisplayConverter _keyConverter = new();

    public string ActionName { get; set; } = "";
    public string DisplayName { get; set; } = "";

    private Key _currentKey;
    public Key CurrentKey
    {
        get => _currentKey;
        set
        {
            if (SetProperty(ref _currentKey, value))
                OnPropertyChanged(nameof(CurrentKeyDisplay));
        }
    }

    private bool _isWaiting;
    public bool IsWaiting
    {
        get => _isWaiting;
        set
        {
            if (SetProperty(ref _isWaiting, value))
                OnPropertyChanged(nameof(CurrentKeyDisplay));
        }
    }

    public string CurrentKeyDisplay
    {
        get
        {
            if (IsWaiting) return "按下一个键...";
            return (string)_keyConverter.Convert(CurrentKey, typeof(string), null, CultureInfo.InvariantCulture);
        }
    }
}
