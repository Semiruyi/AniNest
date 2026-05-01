using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LocalPlayer.Model;

namespace LocalPlayer.View.Behaviors;

public static class MouseGestureBehavior
{
    // ================================================================
    //  附加属性
    // ================================================================

    public static readonly DependencyProperty LeftClickProperty        = RegisterCommand("LeftClick");
    public static readonly DependencyProperty LeftDoubleClickProperty  = RegisterCommand("LeftDoubleClick");
    public static readonly DependencyProperty LeftHoldProperty         = RegisterCommand("LeftHold");
    public static readonly DependencyProperty LeftHoldReleaseProperty  = RegisterCommand("LeftHoldRelease");
    public static readonly DependencyProperty RightClickProperty       = RegisterCommand("RightClick");
    public static readonly DependencyProperty RightDoubleClickProperty = RegisterCommand("RightDoubleClick");
    public static readonly DependencyProperty RightHoldProperty        = RegisterCommand("RightHold");
    public static readonly DependencyProperty RightHoldReleaseProperty = RegisterCommand("RightHoldRelease");
    public static readonly DependencyProperty XButton1Property =
        DependencyProperty.RegisterAttached("XButton1", typeof(ICommand), typeof(MouseGestureBehavior),
            new PropertyMetadata(null, OnXButton1Changed));

    public static ICommand GetLeftClick(DependencyObject o)        => (ICommand)o.GetValue(LeftClickProperty);
    public static void SetLeftClick(DependencyObject o, ICommand v) => o.SetValue(LeftClickProperty, v);
    public static ICommand GetLeftDoubleClick(DependencyObject o)  => (ICommand)o.GetValue(LeftDoubleClickProperty);
    public static void SetLeftDoubleClick(DependencyObject o, ICommand v) => o.SetValue(LeftDoubleClickProperty, v);
    public static ICommand GetLeftHold(DependencyObject o)         => (ICommand)o.GetValue(LeftHoldProperty);
    public static void SetLeftHold(DependencyObject o, ICommand v) => o.SetValue(LeftHoldProperty, v);
    public static ICommand GetLeftHoldRelease(DependencyObject o)  => (ICommand)o.GetValue(LeftHoldReleaseProperty);
    public static void SetLeftHoldRelease(DependencyObject o, ICommand v) => o.SetValue(LeftHoldReleaseProperty, v);
    public static ICommand GetRightClick(DependencyObject o)       => (ICommand)o.GetValue(RightClickProperty);
    public static void SetRightClick(DependencyObject o, ICommand v) => o.SetValue(RightClickProperty, v);
    public static ICommand GetRightDoubleClick(DependencyObject o) => (ICommand)o.GetValue(RightDoubleClickProperty);
    public static void SetRightDoubleClick(DependencyObject o, ICommand v) => o.SetValue(RightDoubleClickProperty, v);
    public static ICommand GetRightHold(DependencyObject o)        => (ICommand)o.GetValue(RightHoldProperty);
    public static void SetRightHold(DependencyObject o, ICommand v) => o.SetValue(RightHoldProperty, v);
    public static ICommand GetRightHoldRelease(DependencyObject o) => (ICommand)o.GetValue(RightHoldReleaseProperty);
    public static void SetRightHoldRelease(DependencyObject o, ICommand v) => o.SetValue(RightHoldReleaseProperty, v);
    public static ICommand GetXButton1(DependencyObject o) => (ICommand)o.GetValue(XButton1Property);
    public static void SetXButton1(DependencyObject o, ICommand v) => o.SetValue(XButton1Property, v);

    public static readonly DependencyProperty HoldDurationProperty =
        DependencyProperty.RegisterAttached("HoldDuration", typeof(int), typeof(MouseGestureBehavior),
            new PropertyMetadata(500));
    public static int GetHoldDuration(DependencyObject o) => (int)o.GetValue(HoldDurationProperty);
    public static void SetHoldDuration(DependencyObject o, int v) => o.SetValue(HoldDurationProperty, v);

    public static readonly DependencyProperty DoubleClickWindowProperty =
        DependencyProperty.RegisterAttached("DoubleClickWindow", typeof(int), typeof(MouseGestureBehavior),
            new PropertyMetadata(GetSystemDoubleClickTime()));
    public static int GetDoubleClickWindow(DependencyObject o) => (int)o.GetValue(DoubleClickWindowProperty);
    public static void SetDoubleClickWindow(DependencyObject o, int v) => o.SetValue(DoubleClickWindowProperty, v);

    private static DependencyProperty RegisterCommand(string name) =>
        DependencyProperty.RegisterAttached(name, typeof(ICommand), typeof(MouseGestureBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    private static void OnXButton1Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement el && _xbuttonSubscribed.Add(el))
        {
            el.PreviewMouseDown += (_, args) =>
            {
                if (args is MouseButtonEventArgs mb && mb.ChangedButton == MouseButton.XButton1)
                {
                    var cmd = GetXButton1(el);
                    if (cmd?.CanExecute(null) == true)
                    {
                        cmd.Execute(null);
                        args.Handled = true;
                    }
                }
            };
            el.Unloaded += (_, _) => _xbuttonSubscribed.Remove(el);
        }
    }

    // ================================================================
    //  订阅
    // ================================================================

    private static readonly HashSet<FrameworkElement> _subscribed = new();
    private static readonly HashSet<FrameworkElement> _xbuttonSubscribed = new();

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement el && _subscribed.Add(el))
        {
            el.SetValue(_stateKey, new ButtonState());
            el.PreviewMouseLeftButtonDown  += (_, e) => OnDown(el, e, left: true);
            el.PreviewMouseLeftButtonUp    += (_, e) => OnUp(el, e, left: true);
            el.PreviewMouseRightButtonDown += (_, e) => OnDown(el, e, left: false);
            el.PreviewMouseRightButtonUp   += (_, e) => OnUp(el, e, left: false);
            el.Unloaded += (_, _) => _subscribed.Remove(el);
        }
    }

    // ================================================================
    //  状态
    // ================================================================

    private static readonly DependencyProperty _stateKey =
        DependencyProperty.RegisterAttached("__State", typeof(ButtonState), typeof(MouseGestureBehavior));

    private static ButtonState GetState(UIElement e) => (ButtonState)e.GetValue(_stateKey);

    private class ButtonState
    {
        public bool Down;
        public bool HoldFired;
        public bool Pending;
        public DispatcherTimer? HoldTimer;
        public DispatcherTimer? WaitTimer;
    }

    // ================================================================
    //  状态机（左右键共用）
    // ================================================================

    private static void OnDown(UIElement el, MouseButtonEventArgs e, bool left)
    {
        var s = GetState(el);
        string tag = Tag(el, left);
        Log($"{tag} ▼ CC={e.ClickCount} down={s.Down} pending={s.Pending} hold={s.HoldFired}");

        if (e.ClickCount >= 2)
        {
            s.Pending = true;
            s.WaitTimer?.Stop();
            return;
        }

        // 新按下 → 重置所有残留状态
        s.WaitTimer?.Stop();
        s.HoldTimer?.Stop();
        s.Down = true;
        s.HoldFired = false;
        s.Pending = false;

        if (GetCmd(el, Hold(left)) != null || GetCmd(el, HoldRelease(left)) != null)
        {
            int dur = GetHoldDuration(el);
            s.HoldTimer = NewTimer(dur, () =>
            {
                if (s.Down && !s.HoldFired)
                {
                    s.HoldFired = true;
                    Log($"{tag} Hold {dur}ms");
                    Execute(GetCmd(el, Hold(left)), GetCommandParameter(el));
                }
            });
            s.HoldTimer.Start();
        }
    }

    private static void OnUp(UIElement el, MouseButtonEventArgs e, bool left)
    {
        var s = GetState(el);
        string tag = Tag(el, left);
        Log($"{tag} ▲ CC={e.ClickCount} down={s.Down} pending={s.Pending} hold={s.HoldFired}");

        s.Down = false;
        s.HoldTimer?.Stop();

        if (s.Pending)
        {
            s.Pending = false;
            Log($"{tag} ▲ → DoubleClick");
            Execute(GetCmd(el, DoubleClick(left)), GetCommandParameter(el));
            return;
        }

        if (s.HoldFired)
        {
            s.HoldFired = false;
            Execute(GetCmd(el, HoldRelease(left)), GetCommandParameter(el));
            return;
        }

        ICommand? doubleCmd = GetCmd(el, DoubleClick(left));
        if (doubleCmd != null)
        {
            int win = GetDoubleClickWindow(el);
            var param = GetCommandParameter(el);
            s.WaitTimer = NewTimer(win, () => Execute(GetCmd(el, Click(left)), param));
            s.WaitTimer.Start();
        }
        else
        {
            Execute(GetCmd(el, Click(left)), GetCommandParameter(el));
        }
    }

    // ================================================================
    //  命令选择
    // ================================================================

    private enum Gesture { Click, DoubleClick, Hold, HoldRelease }

    private static Gesture Click(bool left)       => left ? Gesture.Click : (Gesture)4;
    private static Gesture DoubleClick(bool left) => left ? Gesture.DoubleClick : (Gesture)5;
    private static Gesture Hold(bool left)        => left ? Gesture.Hold : (Gesture)6;
    private static Gesture HoldRelease(bool left) => left ? Gesture.HoldRelease : (Gesture)7;

    private static ICommand? GetCmd(UIElement el, Gesture g) => g switch
    {
        Gesture.Click       => GetLeftClick(el),
        Gesture.DoubleClick => GetLeftDoubleClick(el),
        Gesture.Hold        => GetLeftHold(el),
        Gesture.HoldRelease => GetLeftHoldRelease(el),
        (Gesture)4          => GetRightClick(el),
        (Gesture)5          => GetRightDoubleClick(el),
        (Gesture)6          => GetRightHold(el),
        (Gesture)7          => GetRightHoldRelease(el),
        _                   => null
    };

    // ================================================================
    //  工具
    // ================================================================

    private static string Tag(UIElement el, bool left) => $"{(left ? "L" : "R")}:{el.GetHashCode():X4}";

    private static void Log(string msg) => AppLog.Debug(nameof(MouseGestureBehavior), msg);

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.RegisterAttached("CommandParameter", typeof(object), typeof(MouseGestureBehavior),
            new PropertyMetadata(null));

    public static object? GetCommandParameter(DependencyObject o) => o.GetValue(CommandParameterProperty);
    public static void SetCommandParameter(DependencyObject o, object? v) => o.SetValue(CommandParameterProperty, v);

    private static void Execute(ICommand? cmd, object? param = null)
    {
        if (cmd?.CanExecute(param) == true) cmd.Execute(param);
    }

    private static DispatcherTimer NewTimer(int ms, Action action)
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        t.Tick += (_, _) => { t.Stop(); action(); };
        return t;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    private static int GetSystemDoubleClickTime()
    {
        try { return (int)GetDoubleClickTime(); }
        catch { return 500; }
    }
}
