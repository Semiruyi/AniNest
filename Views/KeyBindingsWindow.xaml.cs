using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using LocalPlayer.Models;
using LocalPlayer.Services;
using WinKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WinKeyEventHandler = System.Windows.Input.KeyEventHandler;

namespace LocalPlayer.Views;

public partial class KeyBindingsWindow : Window
{
    private readonly PlayerInputHandler inputHandler;
    private readonly List<BindingItem> items = new();
    private BindingItem? waitingItem;
    private WinKeyEventHandler? waitingHandler;

    public KeyBindingsWindow(PlayerInputHandler handler)
    {
        InitializeComponent();
        inputHandler = handler;
        LoadBindings();
        KeyBindingsList.ItemsSource = items;
    }

    private void LoadBindings()
    {
        var current = inputHandler.GetCurrentBindings();
        var defaults = PlayerInputHandler.GetDefaultBindings();
        items.Clear();
        foreach (var def in defaults)
        {
            var key = current.TryGetValue(def.ActionName, out var k) ? k : def.DefaultKey;
            items.Add(new BindingItem
            {
                ActionName = def.ActionName,
                DisplayName = def.DisplayName,
                CurrentKey = key
            });
        }
    }

    private void KeyBindingBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not BindingItem item) return;

        CancelWaiting();

        waitingItem = item;
        waitingHandler = (_, e2) =>
        {
            var newKey = e2.Key == Key.System ? e2.SystemKey : e2.Key;

            if (newKey == Key.Escape)
            {
                CancelWaiting();
                return;
            }

            var conflict = items.FirstOrDefault(i => i != item && i.CurrentKey == newKey);
            if (conflict != null)
                conflict.CurrentKey = Key.None;

            item.CurrentKey = newKey;
            inputHandler.SetBinding(item.ActionName, newKey);
            CancelWaiting();
        };

        PreviewKeyDown += waitingHandler;

        item.IsWaiting = true;
    }

    private void CancelWaiting()
    {
        if (waitingHandler != null)
        {
            PreviewKeyDown -= waitingHandler;
            waitingHandler = null;
        }
        if (waitingItem != null)
        {
            waitingItem.IsWaiting = false;
            waitingItem = null;
        }
    }

    private void ResetDefaultBtn_Click(object sender, RoutedEventArgs e)
    {
        var defaults = PlayerInputHandler.GetDefaultBindings();
        foreach (var def in defaults)
            inputHandler.SetBinding(def.ActionName, def.DefaultKey);
        CancelWaiting();
        LoadBindings();
        KeyBindingsList.ItemsSource = null;
        KeyBindingsList.ItemsSource = items;
    }

    private void DoneBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    public class BindingItem : INotifyPropertyChanged
    {
        public string ActionName { get; set; } = "";
        public string DisplayName { get; set; } = "";

        private Key _currentKey;
        public Key CurrentKey
        {
            get => _currentKey;
            set
            {
                _currentKey = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentKeyDisplay));
            }
        }

        private bool _isWaiting;
        public bool IsWaiting
        {
            get => _isWaiting;
            set
            {
                _isWaiting = value;
                OnPropertyChanged();
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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
