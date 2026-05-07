using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using AniNest.Infrastructure.Interop;

namespace AniNest.Presentation.Behaviors;

public static class MouseGestureBehavior
{
    public static readonly DependencyProperty LeftClickProperty =
        RegisterCommand("LeftClick");
    public static readonly DependencyProperty LeftDoubleClickProperty =
        RegisterCommand("LeftDoubleClick");
    public static readonly DependencyProperty RightClickProperty =
        RegisterCommand("RightClick");
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
    public static ICommand GetRightClick(DependencyObject o) => (ICommand)o.GetValue(RightClickProperty);
    public static void SetRightClick(DependencyObject o, ICommand v) => o.SetValue(RightClickProperty, v);
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

    private static readonly HashSet<FrameworkElement> Subscribed = new();

    private static DependencyProperty RegisterCommand(string name) =>
        DependencyProperty.RegisterAttached(name, typeof(ICommand), typeof(MouseGestureBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement el)
            return;

        if (HasAnyCommand(el))
        {
            el.Loaded -= OnLoaded;
            el.Loaded += OnLoaded;
            el.Unloaded -= OnUnloaded;
            el.Unloaded += OnUnloaded;
            AttachInteraction(el);
            return;
        }

        Detach(el, preserveLifecycle: false);
        el.Loaded -= OnLoaded;
        el.Unloaded -= OnUnloaded;
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el)
            return;

        if (!HasAnyCommand(el))
            return;

        Log.Debug($"Loaded: element={DescribeFrameworkElement(el)}");
        AttachInteraction(el);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el)
            return;

        Log.Debug($"Unloaded: element={DescribeFrameworkElement(el)}");
        Detach(el, preserveLifecycle: true);
    }


    private static readonly DependencyProperty StateKey =
        DependencyProperty.RegisterAttached("__State", typeof(ButtonState), typeof(MouseGestureBehavior));

    private static ButtonState GetState(UIElement e) => (ButtonState)e.GetValue(StateKey);

    private const int ClickDelay = 200;

    private class ButtonState
    {
        public DispatcherTimer? ClickTimer;
        public bool SkipNextUp;

        public bool RightDown;
        public bool RightHoldFired;
        public DispatcherTimer? RightHoldTimer;
    }


    private static void OnLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement el || PassThrough(el, e)) return;
        var s = GetState(el);
        Log.Debug(
            $"LDown: element={DescribeElement(el)} handled={e.Handled} clicks={(s.ClickTimer != null ? 1 : 0)} " +
            $"skipNextUp={s.SkipNextUp} original={DescribeSource(e.OriginalSource as DependencyObject)}");

        if (s.ClickTimer != null)
        {
            s.ClickTimer.Stop();
            s.ClickTimer = null;
            s.SkipNextUp = true;
            Log.Debug($"LDown -> DoubleClick: element={DescribeElement(el)}");
            Execute(GetLeftDoubleClick(el), GetCommandParameter(el));
            e.Handled = true;
        }
    }

    private static void OnLeftUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement el || PassThrough(el, e)) return;
        var s = GetState(el);
        Log.Debug(
            $"LUp: element={DescribeElement(el)} handled={e.Handled} skipNextUp={s.SkipNextUp} " +
            $"original={DescribeSource(e.OriginalSource as DependencyObject)}");

        if (s.SkipNextUp)
        {
            s.SkipNextUp = false;
            Log.Debug($"LUp skipped: element={DescribeElement(el)}");
            return;
        }

        s.ClickTimer = NewTimer(ClickDelay, () =>
        {
            s.ClickTimer = null;
            Log.Debug($"LUp -> ClickTimerFired: element={DescribeElement(el)}");
            Execute(GetLeftClick(el), GetCommandParameter(el));
        });
        s.ClickTimer.Start();
        Log.Debug($"LUp -> ClickTimerStart: element={DescribeElement(el)} delay={ClickDelay}");
    }


    private static void OnRightDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement el || PassThrough(el, e)) return;
        var s = GetState(el);
        Log.Debug(
            $"RDown: element={DescribeElement(el)} handled={e.Handled} rightDown={s.RightDown} " +
            $"holdFired={s.RightHoldFired} original={DescribeSource(e.OriginalSource as DependencyObject)}");

        if (s.RightDown || s.RightHoldFired)
        {
            Log.Debug($"RDown -> ResetStaleState: element={DescribeElement(el)}");
            ResetRightState(s);
        }

        s.RightDown = true;
        s.RightHoldFired = false;

        var dur = GetHoldDuration(el);
        s.RightHoldTimer = NewTimer(dur, () =>
        {
            if (s.RightDown && !s.RightHoldFired)
            {
                s.RightHoldFired = true;
                Log.Debug($"RDown -> Hold: element={DescribeElement(el)} duration={dur}");
                Execute(GetRightHold(el), GetCommandParameter(el));
            }
        });
        s.RightHoldTimer.Start();
        Log.Debug($"RDown -> HoldTimerStart: element={DescribeElement(el)} duration={dur}");
    }

    private static void OnRightUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement el || PassThrough(el, e)) return;
        var s = GetState(el);
        Log.Debug(
            $"RUp: element={DescribeElement(el)} handled={e.Handled} rightDown={s.RightDown} holdFired={s.RightHoldFired} " +
            $"original={DescribeSource(e.OriginalSource as DependencyObject)}");

        s.RightDown = false;
        s.RightHoldTimer?.Stop();

        if (s.RightHoldFired)
        {
            s.RightHoldFired = false;
            Log.Debug($"RDown -> HoldRelease: element={DescribeElement(el)}");
            Execute(GetRightHoldRelease(el), GetCommandParameter(el));
            e.Handled = true;
            return;
        }

        var cmd = GetRightClick(el);
        var param = GetCommandParameter(el);
        if (cmd?.CanExecute(param) == true)
        {
            Log.Debug($"RDown -> Click: element={DescribeElement(el)} command={cmd.GetType().Name}");
            cmd.Execute(param);
            e.Handled = true;
        }
        else
        {
            Log.Debug($"RDown -> Click skipped: element={DescribeElement(el)} command={(cmd == null ? "null" : cmd.GetType().Name)}");
        }

        ResetRightState(s);
    }

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

    private static bool HasAnyCommand(FrameworkElement el)
    {
        return GetLeftClick(el) != null ||
               GetLeftDoubleClick(el) != null ||
               GetRightClick(el) != null ||
               GetRightHold(el) != null ||
               GetRightHoldRelease(el) != null ||
               GetXButton1(el) != null;
    }

    private static void AttachInteraction(FrameworkElement el)
    {
        if (!Subscribed.Add(el))
            return;

        if (el.ReadLocalValue(StateKey) is not ButtonState)
            el.SetValue(StateKey, new ButtonState());

        Log.Debug($"Attach: element={DescribeFrameworkElement(el)}");
        el.PreviewMouseLeftButtonDown += OnLeftDown;
        el.PreviewMouseLeftButtonUp += OnLeftUp;
        el.PreviewMouseRightButtonDown += OnRightDown;
        el.PreviewMouseRightButtonUp += OnRightUp;
        el.PreviewMouseDown += OnXButton;
    }

    private static void Detach(FrameworkElement el, bool preserveLifecycle)
    {
        Log.Debug($"Detach: element={DescribeElement(el)}");
        el.PreviewMouseLeftButtonDown -= OnLeftDown;
        el.PreviewMouseLeftButtonUp -= OnLeftUp;
        el.PreviewMouseRightButtonDown -= OnRightDown;
        el.PreviewMouseRightButtonUp -= OnRightUp;
        el.PreviewMouseDown -= OnXButton;

        if (el.ReadLocalValue(StateKey) is ButtonState state)
        {
            state.ClickTimer?.Stop();
            state.ClickTimer = null;
            ResetRightState(state);
            state.SkipNextUp = false;
        }

        Subscribed.Remove(el);

        if (!preserveLifecycle)
            el.ClearValue(StateKey);
    }

    private static string DescribeElement(UIElement element)
    {
        if (element is FrameworkElement frameworkElement)
            return DescribeFrameworkElement(frameworkElement);

        return element.GetType().Name;
    }

    private static string DescribeFrameworkElement(FrameworkElement frameworkElement)
    {
        var name = string.IsNullOrWhiteSpace(frameworkElement.Name) ? "-" : frameworkElement.Name;
        return $"{frameworkElement.GetType().Name}({name})";
    }

    private static string DescribeSource(DependencyObject? source)
    {
        if (source == null)
            return "null";

        if (source is FrameworkElement frameworkElement)
        {
            var name = string.IsNullOrWhiteSpace(frameworkElement.Name) ? "-" : frameworkElement.Name;
            return $"{frameworkElement.GetType().Name}({name})";
        }

        return source.GetType().Name;
    }

    public static void ResetRightState(DependencyObject element)
    {
        if (element is not UIElement uiElement)
            return;

        if (uiElement.ReadLocalValue(StateKey) is not ButtonState state)
            return;

        Log.Debug($"ResetRightState: element={DescribeElement(uiElement)}");
        ResetRightState(state);
    }

    private static void ResetRightState(ButtonState state)
    {
        state.RightHoldTimer?.Stop();
        state.RightHoldTimer = null;
        state.RightDown = false;
        state.RightHoldFired = false;
    }
}




