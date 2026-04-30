using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalPlayer.Media;
using LocalPlayer.Model;

namespace LocalPlayer.ViewModel;

public partial class KeyBindingsViewModel : ObservableObject
{
    private static void Log(string message) => AppLog.Info(nameof(KeyBindingsViewModel), message);

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
            if (CurrentKey == Key.None) return "(未绑定)";
            var name = CurrentKey.ToString();
            return name
                .Replace("Left", "←")
                .Replace("Right", "→")
                .Replace("Up", "↑")
                .Replace("Down", "↓")
                .Replace("Space", "空格")
                .Replace("Escape", "Esc")
                .Replace("Return", "Enter")
                .Replace("PageUp", "PgUp")
                .Replace("PageDown", "PgDn")
                .Replace("OemComma", ",")
                .Replace("OemPeriod", ".")
                .Replace("OemMinus", "-")
                .Replace("OemPlus", "=")
                .Replace("OemQuestion", "/")
                .Replace("OemSemicolon", ";")
                .Replace("OemQuotes", "\"")
                .Replace("OemOpenBrackets", "[")
                .Replace("OemCloseBrackets", "]")
                .Replace("OemPipe", "\\")
                .Replace("OemTilde", "~")
                .Replace("D0", "0").Replace("D1", "1").Replace("D2", "2")
                .Replace("D3", "3").Replace("D4", "4").Replace("D5", "5")
                .Replace("D6", "6").Replace("D7", "7").Replace("D8", "8")
                .Replace("D9", "9")
                .Replace("NumPad0", "Num0").Replace("NumPad1", "Num1")
                .Replace("NumPad2", "Num2").Replace("NumPad3", "Num3")
                .Replace("NumPad4", "Num4").Replace("NumPad5", "Num5")
                .Replace("NumPad6", "Num6").Replace("NumPad7", "Num7")
                .Replace("NumPad8", "Num8").Replace("NumPad9", "Num9")
                .Replace("Add", "+").Replace("Subtract", "-")
                .Replace("Multiply", "*").Replace("Divide", "/")
                .Replace("Decimal", ".");
        }
    }
}
