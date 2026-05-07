using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GeneratorStatus = System.Windows.Controls.Primitives.GeneratorStatus;

namespace AniNest.Presentation.Animations;

public static class StaggeredItemsAnimation
{
    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(StaggeredItemsAnimation),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject o) => (bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o, bool v) => o.SetValue(EnabledProperty, v);

    public static readonly DependencyProperty StaggerMsProperty =
        DependencyProperty.RegisterAttached("StaggerMs", typeof(int), typeof(StaggeredItemsAnimation),
            new PropertyMetadata(50));

    public static int GetStaggerMs(DependencyObject o) => (int)o.GetValue(StaggerMsProperty);
    public static void SetStaggerMs(DependencyObject o, int v) => o.SetValue(StaggerMsProperty, v);

    public static readonly DependencyProperty DurationMsProperty =
        DependencyProperty.RegisterAttached("DurationMs", typeof(int), typeof(StaggeredItemsAnimation),
            new PropertyMetadata(300));

    public static int GetDurationMs(DependencyObject o) => (int)o.GetValue(DurationMsProperty);
    public static void SetDurationMs(DependencyObject o, int v) => o.SetValue(DurationMsProperty, v);

    private static readonly DependencyProperty NextIndexProperty =
        DependencyProperty.RegisterAttached("NextIndex", typeof(int), typeof(StaggeredItemsAnimation),
            new PropertyMetadata(0));

    private static readonly DependencyProperty CollectionChangedHandlerProperty =
        DependencyProperty.RegisterAttached("CollectionChangedHandler", typeof(NotifyCollectionChangedEventHandler), typeof(StaggeredItemsAnimation));

    private static readonly DependencyProperty CollectionChangedSourceProperty =
        DependencyProperty.RegisterAttached("CollectionChangedSource", typeof(INotifyCollectionChanged), typeof(StaggeredItemsAnimation));

    private static readonly DependencyProperty StatusChangedHandlerProperty =
        DependencyProperty.RegisterAttached("StatusChangedHandler", typeof(EventHandler), typeof(StaggeredItemsAnimation));

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl ic) return;
        ic.Loaded -= OnLoaded;
        ic.Unloaded -= OnUnloaded;
        if (e.NewValue is true)
        {
            ic.Loaded += OnLoaded;
            ic.Unloaded += OnUnloaded;
        }
        else
        {
            Detach(ic);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var ic = (ItemsControl)sender;
        ic.SetValue(NextIndexProperty, 0);

        Detach(ic);

        EventHandler statusChangedHandler = (_, _) => AnimatePending(ic);
        ic.ItemContainerGenerator.StatusChanged += statusChangedHandler;
        ic.SetValue(StatusChangedHandlerProperty, statusChangedHandler);

        if (ic.ItemsSource is INotifyCollectionChanged ncc)
        {
            NotifyCollectionChangedEventHandler collectionChangedHandler = (_, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Add)
                {
                    ic.Dispatcher.BeginInvoke(
                        new Action(() => AnimatePending(ic)),
                        DispatcherPriority.Loaded);
                }
                else if (args.Action == NotifyCollectionChangedAction.Remove)
                {
                    int nextIdx = (int)ic.GetValue(NextIndexProperty);
                    if (args.OldStartingIndex < nextIdx)
                        ic.SetValue(NextIndexProperty, nextIdx - 1);
                }
                else if (args.Action == NotifyCollectionChangedAction.Reset)
                {
                    ic.SetValue(NextIndexProperty, 0);
                }
            };
            ncc.CollectionChanged += collectionChangedHandler;
            ic.SetValue(CollectionChangedHandlerProperty, collectionChangedHandler);
            ic.SetValue(CollectionChangedSourceProperty, ncc);
        }

        AnimatePending(ic);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ItemsControl ic)
            Detach(ic);
    }

    private static void Detach(ItemsControl ic)
    {
        var statusChangedValue = ic.ReadLocalValue(StatusChangedHandlerProperty);
        if (!ReferenceEquals(statusChangedValue, DependencyProperty.UnsetValue) &&
            statusChangedValue is EventHandler statusChangedHandler)
        {
            ic.ItemContainerGenerator.StatusChanged -= statusChangedHandler;
            ic.ClearValue(StatusChangedHandlerProperty);
        }

        var collectionChangedValue = ic.ReadLocalValue(CollectionChangedHandlerProperty);
        var collectionChangedSourceValue = ic.ReadLocalValue(CollectionChangedSourceProperty);
        if (!ReferenceEquals(collectionChangedValue, DependencyProperty.UnsetValue) &&
            !ReferenceEquals(collectionChangedSourceValue, DependencyProperty.UnsetValue) &&
            collectionChangedValue is NotifyCollectionChangedEventHandler collectionChangedHandler &&
            collectionChangedSourceValue is INotifyCollectionChanged collectionChangedSource)
        {
            collectionChangedSource.CollectionChanged -= collectionChangedHandler;
            ic.ClearValue(CollectionChangedHandlerProperty);
            ic.ClearValue(CollectionChangedSourceProperty);
        }

        ic.ClearValue(NextIndexProperty);
    }

    private static void AnimatePending(ItemsControl ic)
    {
        if (ic.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
            return;

        int stagger = GetStaggerMs(ic);
        int index = (int)ic.GetValue(NextIndexProperty);

        while (index < ic.Items.Count)
        {
            var container = ic.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
            if (container == null) break;
            AnimationHelper.ApplyEntrance(container, EntranceEffect.Default, index * stagger);
            index++;
        }

        ic.SetValue(NextIndexProperty, index);
    }
}

