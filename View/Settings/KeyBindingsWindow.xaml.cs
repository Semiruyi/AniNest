using System;
using System.Windows;
using System.Windows.Input;
using LocalPlayer.Model;
using LocalPlayer.ViewModel;
using WinKeyEventArgs= System.Windows.Input.KeyEventArgs;
using WinKeyEventHandler = System.Windows.Input.KeyEventHandler;

namespace LocalPlayer.View.Settings;

public partial class KeyBindingsWindow : Window
{
    private static void Log(string msg) => AppLog.Info(nameof(KeyBindingsWindow), msg);

    private readonly KeyBindingsViewModel _vm;
    private BindingItem? waitingItem;
    private WinKeyEventHandler? waitingHandler;
    private bool isProcessing;

    public KeyBindingsWindow(PlayerInputHandler handler)
    {
        InitializeComponent();
        _vm = new KeyBindingsViewModel(handler);
        DataContext = _vm;
        KeyBindingsList.ItemsSource = _vm.Items;
    }

    private void KeyBindingBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not BindingItem item) return;
        if (isProcessing) return;

        Log($"KeyBindingBtn_Click: 开始等待按键, ActionName={item.ActionName}, 当前绑定={item.CurrentKey}");
        CancelWaiting();

        waitingItem = item;
        waitingItem.IsWaiting = true;
        waitingHandler = (_, e2) =>
        {
            e2.Handled = true;
            Log($"waitingHandler 触发: Key={e2.Key}");
            CancelWaiting();

            var newKey = e2.Key == Key.System ? e2.SystemKey : e2.Key;
            Log($"waitingHandler: 解析后 newKey={newKey}");

            if (newKey == Key.Escape || newKey == Key.None)
            {
                Log("waitingHandler: 取消绑定 (Esc/None)");
                return;
            }

            var capturedItem = item;
            var capturedKey = newKey;
            isProcessing = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Log($"BeginInvoke: 开始处理绑定 {capturedItem.ActionName} = {capturedKey}");
                _vm.TrySetBinding(capturedItem, capturedKey);
                isProcessing = false;
                Log("BeginInvoke: 完成");
            }));
        };

        PreviewKeyDown += waitingHandler;
        Log("KeyBindingBtn_Click: 已注册 PreviewKeyDown");
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
