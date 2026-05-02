using System;
using System.Collections.Generic;
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

    public static readonly DependencyProperty LeftClickProperty =
        RegisterCommand("LeftClick");
    public static readonly DependencyProperty LeftDoubleClickProperty =
        RegisterCommand("LeftDoubleClick");
    public static readonly DependencyProperty RightHoldProperty =
        RegisterCommand("RightHold");
    public static readonly DependencyProperty RightHoldReleaseProperty =
        RegisterCommand("RightHoldRelease");
    public static readonly DependencyProperty XButton1Property =
        RegisterCommand("XButton1");

    public static ICommand GetLeftClick(DependencyObject o) => (ICommand)o.GetValue(LeftClickProperty);
    public static void SetLeftClick(DependencyObject o, ICommand v) => o.SetValue(LeftClickProperty, v);
    public static ICommand GetLeftDoubleClick(DependencyObject o) => (ICommand)o.GetValue(LeftDoubleClickProperty);
    public static void SetLeftDoubleClick(DependencyObject o, ICommand v) => o.SetValue(LeftDoubleClickProperty, v);
    public static ICommand GetRightHold(DependencyObject o) => (ICommand)o.GetValue(RightHoldProperty);
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

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.RegisterAttached("CommandParameter", typeof(object), typeof(MouseGestureBehavior),
            new PropertyMetadata(null));

    public static object? GetCommandParameter(DependencyObject o) => o.GetValue(CommandParameterProperty);
    public static void SetCommandParameter(DependencyObject o, object? v) => o.SetValue(CommandParameterProperty, v);

    // ================================================================
    //  订阅
    // ================================================================

    private static readonly HashSet<FrameworkElement> Subscribed = new();

    private static DependencyProperty RegisterCommand(string name) =>
        DependencyProperty.RegisterAttached(name, typeof(ICommand), typeof(MouseGestureBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement el && Subscribed.Add(el))
        {
            el.SetValue(StateKey, new ButtonState());
            el.PreviewMouseLeftButtonDown += OnLeftDown;
            el.PreviewMouseLeftButtonUp += OnLeftUp;
            el.PreviewMouseRightButtonDown += OnRightDown;
            el.PreviewMouseRightButtonUp += OnRightUp;
            el.PreviewMouseDown += OnXButton;
            el.Unloaded += (_, _) => Subscribed.Remove(el);
        }
    }

    // ================================================================
    //  状态
    // ================================================================

    private static readonly DependencyProperty StateKey =
        DependencyProperty.RegisterAttached("__State", typeof(ButtonState), typeof(MouseGestureBehavior));

    private static ButtonState GetState(UIElement e) => (ButtonState)e.GetValue(StateKey);

    /// <summary>
    /// 单击延迟 (ms) — 区分单击和双击
    /// </summary>
    private const int ClickDelay = 200;

    private class ButtonState
    {
        /// <summary>左键：单击后等待 200ms 以区分双击的计时器</summary>
        public DispatcherTimer? ClickTimer;
        /// <summary>左键：双击已在按下时触发，跳过紧接着的弹起事件</summary>
        public bool SkipNextUp;

        /// <summary>右键：按下中</summary>
        public bool RightDown;
        /// <summary>右键：长按已触发</summary>
        public bool RightHoldFired;
        /// <summary>右键：长按计时器</summary>
        public DispatcherTimer? RightHoldTimer;
    }

    // ================================================================
    //  左键：单击 / 双击
    // ================================================================

    private static void OnLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement el || PassThrough(el, e)) return;
        var s = GetState(el);

        // 单击计时器运行中 → 200ms 内再次按下，判定为双击
        if (s.ClickTimer != null)
        {
            s.ClickTimer.Stop();
            s.ClickTimer = null;
            s.SkipNextUp = true;
            Log.Debug("L▼ → DoubleClick");
            Execute(GetLeftDoubleClick(el), GetCommandParameter(el));
            e.Handled = true;
        }
    }

    private static void OnLeftUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement el || PassThrough(el, e)) return;
        var s = GetState(el);

        // 双击已触发，忽略紧随的弹起
        if (s.SkipNextUp)
        {
            s.SkipNextUp = false;
            return;
        }

        // 首次单击弹起：启动 200ms 计时器，超时无再次按下则触发单击
        s.ClickTimer = NewTimer(ClickDelay, () =>
        {
            s.ClickTimer = null;
            Log.Debug("L▲ → Click");
            Execute(GetLeftClick(el), GetCommandParameter(el));
        });
        s.ClickTimer.Start();
    }

    // ================================================================
    //  右键：长按
    // ================================================================

    private static void OnRightDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement el || PassThrough(el, e)) return;
        var s = GetState(el);
        Log.Debug("R▼");

        s.RightDown = true;
        s.RightHoldFired = false;
        s.RightHoldTimer?.Stop();

        var dur = GetHoldDuration(el);
        s.RightHoldTimer = NewTimer(dur, () =>
        {
            if (s.RightDown && !s.RightHoldFired)
            {
                s.RightHoldFired = true;
                Log.Debug($"R▼ → Hold ({dur}ms)");
                Execute(GetRightHold(el), GetCommandParameter(el));
            }
        });
        s.RightHoldTimer.Start();
    }

    private static void OnRightUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement el || PassThrough(el, e)) return;
        var s = GetState(el);
        Log.Debug("R▲");

        s.RightDown = false;
        s.RightHoldTimer?.Stop();

        if (s.RightHoldFired)
        {
            s.RightHoldFired = false;
            Log.Debug("R▲ → HoldRelease");
            Execute(GetRightHoldRelease(el), GetCommandParameter(el));
        }
    }

    // ================================================================
    //  XButton1（鼠标侧键前进）
    // ================================================================

    private static void OnXButton(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement el || e.ChangedButton != MouseButton.XButton1) return;
        var cmd = GetXButton1(el);
        if (cmd?.CanExecute(null) == true)
        {
            cmd.Execute(null);
            e.Handled = true;
        }
    }

    // ================================================================
    //  工具
    // ================================================================

    private static readonly Logger Log = AppLog.For(nameof(MouseGestureBehavior));

    private static bool PassThrough(UIElement el, MouseButtonEventArgs e) =>
        e.OriginalSource is DependencyObject d && AncestorIsButton(d, el);

    private static bool AncestorIsButton(DependencyObject source, DependencyObject boundary)
    {
        var current = source;
        while (current != null && current != boundary)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase)
                return true;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return false;
    }

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
}
