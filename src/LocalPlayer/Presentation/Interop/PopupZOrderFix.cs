using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace LocalPlayer.Presentation.Interop;

public static class PopupZOrderFix
{
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int GWLP_HWNDPARENT = -8;
    private static readonly nint HWND_NOTOPMOST = -2;
    private const uint SWP_NOACTIVATE = 0x0010;

    public static void Apply(Popup popup)
    {
        popup.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!popup.IsOpen || popup.Child is null) return;
            var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
            if (source is null || source.Handle == 0) return;
            var h = source.Handle;

            int exStyle = GetWindowLong(h, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOPMOST) != 0)
                SetWindowLong(h, GWL_EXSTYLE, exStyle & ~WS_EX_TOPMOST);

            var window = Window.GetWindow(popup.PlacementTarget ?? popup.Child)
                      ?? Application.Current?.MainWindow;
            if (window != null)
                SetWindowLongPtr(h, GWLP_HWNDPARENT, new WindowInteropHelper(window).Handle);

            if (GetWindowRect(h, out var rc))
                SetWindowPos(h, HWND_NOTOPMOST,
                    rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top,
                    SWP_NOACTIVATE);
        }), System.Windows.Threading.DispatcherPriority.Background);
    }
}

