using System.Runtime.InteropServices;

namespace LocalPlayer.Infrastructure.Model;

public static class TaskbarHelper
{
    private static readonly Logger Log = AppLog.For("Taskbar");

    [DllImport("shell32.dll")]
    private static extern IntPtr SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("kernel32.dll")]
    private static extern uint GetLastError();

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uCallbackMessage;
        public int uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    private const int ABM_GETSTATE = 0x04;
    private const int ABM_SETSTATE = 0x0A;
    private const int ABS_AUTOHIDE = 0x1;
    private const int ABS_ALWAYSONTOP = 0x2;

    private static IntPtr TaskbarHandle
    {
        get
        {
            var hwnd = FindWindow("Shell_TrayWnd", null!);
            Log.Debug($"TaskbarHandle = 0x{hwnd.ToInt64():X}");
            return hwnd;
        }
    }

    public static bool IsAutoHideEnabled
    {
        get
        {
            var abd = new APPBARDATA();
            abd.cbSize = Marshal.SizeOf(abd);
            abd.hWnd = TaskbarHandle;

            var ret = SHAppBarMessage(ABM_GETSTATE, ref abd);
            var state = ret.ToInt32();
            var enabled = (state & ABS_AUTOHIDE) != 0;

            Log.Debug($"GETSTATE: ret=0x{state:X}, autoHide={enabled}, alwaysOnTop={(state & ABS_ALWAYSONTOP) != 0}");
            return enabled;
        }
    }

    public static void EnableAutoHide()
    {
        var abd = BuildAppBarData(ABS_AUTOHIDE | ABS_ALWAYSONTOP);
        Log.Info($"SETSTATE: lParam=0x{abd.lParam.ToInt64():X} (autoHide=1, alwaysOnTop=1)");
        SHAppBarMessage(ABM_SETSTATE, ref abd);
        Log.Debug($"SETSTATE done, lastError={GetLastError()}");
    }

    public static Task EnableAutoHideAsync()
        => Task.Run(EnableAutoHide);

    public static void DisableAutoHide()
    {
        var abd = BuildAppBarData(ABS_ALWAYSONTOP);
        Log.Info($"SETSTATE: lParam=0x{abd.lParam.ToInt64():X} (autoHide=0, alwaysOnTop=1)");
        SHAppBarMessage(ABM_SETSTATE, ref abd);
        Log.Debug($"SETSTATE done, lastError={GetLastError()}");
    }

    public static Task DisableAutoHideAsync()
        => Task.Run(DisableAutoHide);

    private static APPBARDATA BuildAppBarData(int state)
    {
        var abd = new APPBARDATA();
        abd.cbSize = Marshal.SizeOf(abd);
        abd.hWnd = TaskbarHandle;
        abd.lParam = state;
        return abd;
    }

    public static void ToggleAutoHide()
    {
        Log.Info("ToggleAutoHide called");
        if (IsAutoHideEnabled)
            DisableAutoHide();
        else
            EnableAutoHide();
    }
}

