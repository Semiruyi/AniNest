using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace AniNest.Presentation.Animations;

public static class SelectionHighlightAnimation
{
    private sealed class State
    {
        public FrameworkElement? Target;
        public EventHandler? LayoutUpdatedHandler;
        public bool IsLoaded;
        public bool UpdateQueued;
        public bool HasPosition;
    }

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SelectionHighlightAnimation),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty TargetProperty =
        DependencyProperty.RegisterAttached(
            "Target",
            typeof(FrameworkElement),
            typeof(SelectionHighlightAnimation),
            new PropertyMetadata(null, OnTargetChanged));

    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.RegisterAttached(
            "SelectedIndex",
            typeof(int),
            typeof(SelectionHighlightAnimation),
            new PropertyMetadata(-1, OnSelectedIndexChanged));

    public static readonly DependencyProperty DurationMsProperty =
        DependencyProperty.RegisterAttached(
            "DurationMs",
            typeof(int),
            typeof(SelectionHighlightAnimation),
            new PropertyMetadata(220));

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State",
            typeof(State),
            typeof(SelectionHighlightAnimation),
            new PropertyMetadata(null));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static FrameworkElement? GetTarget(DependencyObject obj) => (FrameworkElement?)obj.GetValue(TargetProperty);
    public static void SetTarget(DependencyObject obj, FrameworkElement? value) => obj.SetValue(TargetProperty, value);

    public static int GetSelectedIndex(DependencyObject obj) => (int)obj.GetValue(SelectedIndexProperty);
    public static void SetSelectedIndex(DependencyObject obj, int value) => obj.SetValue(SelectedIndexProperty, value);

    public static int GetDurationMs(DependencyObject obj) => (int)obj.GetValue(DurationMsProperty);
    public static void SetDurationMs(DependencyObject obj, int value) => obj.SetValue(DurationMsProperty, value);

    public static void Invalidate(FrameworkElement highlight)
    {
        if (!GetIsEnabled(highlight))
            return;

        var state = GetOrCreateState(highlight);
        if (!highlight.IsLoaded)
            return;

        state.IsLoaded = true;
        UpdateTargetSubscription(highlight, state);
        QueueUpdate(highlight, state);
    }

    public static void InvalidateDescendants(DependencyObject root)
    {
        if (root is FrameworkElement highlight && GetIsEnabled(highlight))
            Invalidate(highlight);

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            InvalidateDescendants(VisualTreeHelper.GetChild(root, i));
    }

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement highlight)
            return;

        if ((bool)e.NewValue)
        {
            highlight.Loaded += OnHighlightLoaded;
            highlight.Unloaded += OnHighlightUnloaded;
            if (highlight.IsLoaded)
            {
                var activeState = GetOrCreateState(highlight);
                activeState.IsLoaded = true;
                UpdateTargetSubscription(highlight, activeState);
                QueueUpdate(highlight, activeState);
            }

            return;
        }

        highlight.Loaded -= OnHighlightLoaded;
        highlight.Unloaded -= OnHighlightUnloaded;
        var state = GetOrCreateState(highlight);
        state.IsLoaded = false;
        state.UpdateQueued = false;
        state.HasPosition = false;
        highlight.Visibility = Visibility.Collapsed;
        UnsubscribeTarget(highlight, state);
    }

    private static void OnHighlightLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement highlight)
            return;

        var state = GetOrCreateState(highlight);
        state.IsLoaded = true;
        UpdateTargetSubscription(highlight, state);
        QueueUpdate(highlight, state);
    }

    private static void OnHighlightUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement highlight)
            return;

        var state = GetOrCreateState(highlight);
        state.IsLoaded = false;
        state.UpdateQueued = false;
        state.HasPosition = false;
        UnsubscribeTarget(highlight, state);
    }

    private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement highlight || !GetIsEnabled(highlight))
            return;

        var state = GetOrCreateState(highlight);
        UpdateTargetSubscription(highlight, state);
        QueueUpdate(highlight, state);
    }

    private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement highlight || !GetIsEnabled(highlight))
            return;

        QueueUpdate(highlight, GetOrCreateState(highlight));
    }

    private static State GetOrCreateState(DependencyObject obj)
    {
        if (obj.GetValue(StateProperty) is not State state)
        {
            state = new State();
            obj.SetValue(StateProperty, state);
        }

        return state;
    }

    private static void UpdateTargetSubscription(FrameworkElement highlight, State state)
    {
        var nextTarget = GetTarget(highlight);
        if (ReferenceEquals(state.Target, nextTarget))
            return;

        UnsubscribeTarget(highlight, state);
        state.Target = nextTarget;

        if (nextTarget == null)
            return;

        state.LayoutUpdatedHandler ??= (_, _) => QueueUpdate(highlight, state);
        nextTarget.LayoutUpdated += state.LayoutUpdatedHandler;
    }

    private static void UnsubscribeTarget(FrameworkElement highlight, State state)
    {
        if (state.Target != null && state.LayoutUpdatedHandler != null)
            state.Target.LayoutUpdated -= state.LayoutUpdatedHandler;

        state.Target = null;
    }

    private static void QueueUpdate(FrameworkElement highlight, State state)
    {
        if (!state.IsLoaded || state.UpdateQueued)
            return;

        state.UpdateQueued = true;
        highlight.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                state.UpdateQueued = false;
                UpdateHighlight(highlight, state);
            }));
    }

    private static void UpdateHighlight(FrameworkElement highlight, State state)
    {
        int selectedIndex = GetSelectedIndex(highlight);
        if (selectedIndex < 0)
        {
            highlight.Visibility = Visibility.Collapsed;
            return;
        }

        var itemsHost = ResolveItemsHost(GetTarget(highlight));
        if (itemsHost == null || selectedIndex >= itemsHost.Children.Count)
        {
            highlight.Visibility = Visibility.Collapsed;
            return;
        }

        if (VisualTreeHelper.GetParent(highlight) is not UIElement parentElement)
            return;

        if (itemsHost.Children[selectedIndex] is not FrameworkElement selectedElement ||
            selectedElement.ActualWidth <= 0 ||
            selectedElement.ActualHeight <= 0)
        {
            return;
        }

        if (!IsEligibleForHighlightLayout(itemsHost) ||
            parentElement is not FrameworkElement parentFrameworkElement ||
            !IsEligibleForHighlightLayout(parentFrameworkElement) ||
            !IsEligibleForHighlightLayout(selectedElement))
        {
            return;
        }

        var position = selectedElement.TranslatePoint(new Point(0, 0), parentElement);
        var transform = EnsureTranslateTransform(highlight);
        highlight.Visibility = Visibility.Visible;

        bool animate = state.HasPosition;
        state.HasPosition = true;

        AnimateOrSet(highlight, FrameworkElement.WidthProperty, highlight.ActualWidth, selectedElement.ActualWidth, animate, highlight);
        AnimateOrSet(highlight, FrameworkElement.HeightProperty, highlight.ActualHeight, selectedElement.ActualHeight, animate, highlight);
        AnimateOrSet(transform, TranslateTransform.XProperty, transform.X, position.X, animate, highlight);
        AnimateOrSet(transform, TranslateTransform.YProperty, transform.Y, position.Y, animate, highlight);
    }

    private static Panel? ResolveItemsHost(FrameworkElement? target)
    {
        if (target == null)
            return null;

        if (target is Panel panel)
            return panel;

        if (target is ItemsControl itemsControl)
            return FindDescendant<Panel>(itemsControl);

        return FindDescendant<Panel>(target);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                return match;

            var nested = FindDescendant<T>(child);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static TranslateTransform EnsureTranslateTransform(FrameworkElement highlight)
    {
        if (highlight.RenderTransform is TranslateTransform existing)
            return existing;

        var transform = new TranslateTransform();
        highlight.RenderTransform = transform;
        return transform;
    }

    private static void AnimateOrSet(
        DependencyObject target,
        DependencyProperty property,
        double currentValue,
        double nextValue,
        bool animate,
        FrameworkElement owner)
    {
        if (!animate || Math.Abs(currentValue - nextValue) < 0.1)
        {
            target.SetValue(property, nextValue);
            return;
        }

        var animation = new DoubleAnimation
        {
            To = nextValue,
            Duration = TimeSpan.FromMilliseconds(GetDurationMs(owner)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        if (target is Animatable animatable)
        {
            animatable.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
            return;
        }

        target.SetValue(property, nextValue);
    }

    private static bool IsEligibleForHighlightLayout(UIElement element)
        => element.Visibility == Visibility.Visible &&
           element.IsVisible &&
           (!(element is FrameworkElement frameworkElement) ||
            (frameworkElement.ActualWidth > 0 && frameworkElement.ActualHeight > 0));
}
