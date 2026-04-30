using System;
using System.Windows;
using System.Windows.Input;
using LocalPlayer.ViewModel;
using WinKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WinKeyEventHandler = System.Windows.Input.KeyEventHandler;

namespace LocalPlayer.View.Settings;

public partial class KeyBindingsWindow : Window
{
    private readonly KeyBindingsViewModel _vm;
    private BindingItem? waitingItem;
    private WinKeyEventHandler? waitingHandler;
    private bool isProcessing;

    public KeyBindingsWindow(KeyBindingsViewModel vm)
    {
        _vm = vm;
        DataContext = _vm;
        InitializeComponent();
        KeyBindingsList.ItemsSource = _vm.Items;
        KeyBindingsViewModel.Log("快捷键设置窗口已打开");
    }

    private void KeyBindingBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not BindingItem item) return;
        if (isProcessing) return;

        CancelWaiting();

        waitingItem = item;
        waitingItem.IsWaiting = true;
        waitingHandler = (_, e2) =>
        {
            e2.Handled = true;
            CancelWaiting();

            var newKey = e2.Key == Key.System ? e2.SystemKey : e2.Key;

            if (newKey == Key.Escape || newKey == Key.None)
                return;

            var capturedItem = item;
            var capturedKey = newKey;
            isProcessing = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _vm.TrySetBinding(capturedItem, capturedKey);
                isProcessing = false;
            }));
        };

        PreviewKeyDown += waitingHandler;
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
        CancelWaiting();
        _vm.ResetDefaultsCommand.Execute(null);
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
}
