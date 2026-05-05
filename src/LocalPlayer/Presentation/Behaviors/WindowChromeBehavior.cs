using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace LocalPlayer.Presentation.Behaviors;

public static class WindowChromeBehavior
{
    public static readonly DependencyProperty IsDarkTitleBarProperty =
        DependencyProperty.RegisterAttached("IsDarkTitleBar", typeof(bool), typeof(WindowChromeBehavior),
            new PropertyMetadata(false, OnChromeChanged));

    public static readonly DependencyProperty CaptionColorProperty =
        DependencyProperty.RegisterAttached("CaptionColor", typeof(string), typeof(WindowChromeBehavior),
            new PropertyMetadata(null, OnChromeChanged));

    public static bool GetIsDarkTitleBar(DependencyObject o) => (bool)o.GetValue(IsDarkTitleBarProperty);
    public static void SetIsDarkTitleBar(DependencyObject o, bool v) => o.SetValue(IsDarkTitleBarProperty, v);

    public static string? GetCaptionColor(DependencyObject o) => (string?)o.GetValue(CaptionColorProperty);
    public static void SetCaptionColor(DependencyObject o, string? v) => o.SetValue(CaptionColorProperty, v);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

    private static void OnChromeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window) return;

        // Detach/re-attach to avoid duplicate subscription
        window.Loaded -= ApplyChrome;
        window.Loaded += ApplyChrome;
    }

    private static void ApplyChrome(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window) return;
        nint hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;

        if (GetIsDarkTitleBar(window))
        {
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
        }

        var colorStr = GetCaptionColor(window);
        if (colorStr != null)
        {
            int color = ParseHexColor(colorStr);
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref color, sizeof(int));
        }
    }

    private static int ParseHexColor(string hex)
    {
        if (hex.StartsWith("#"))
            hex = hex.Substring(1);
        return int.Parse(hex, System.Globalization.NumberStyles.HexNumber);
    }
}

