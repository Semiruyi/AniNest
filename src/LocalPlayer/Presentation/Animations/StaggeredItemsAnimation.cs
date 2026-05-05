using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GeneratorStatus = System.Windows.Controls.Primitives.GeneratorStatus;

namespace LocalPlayer.Presentation.Animations;

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

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl ic) return;
        ic.Loaded -= OnLoaded;
        if (e.NewValue is true)
            ic.Loaded += OnLoaded;
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var ic = (ItemsControl)sender;
        ic.Loaded -= OnLoaded;
        ic.SetValue(NextIndexProperty, 0);

        ic.ItemContainerGenerator.StatusChanged += (_, _) => AnimatePending(ic);

        // 鍒濆鍔犺浇鍚?generator 涓€鐩村浜?ContainersGenerated锛屾柊澧炲崟涓?item 涓嶆敼鍙樼姸鎬侊紝
        // StatusChanged 涓嶄細鍐嶈Е鍙戯紝鍥犳鐩存帴鐩戝惉闆嗗悎鍙樺寲鏉ユ崟鑾峰悗缁殑 Add銆?
        if (ic.ItemsSource is INotifyCollectionChanged ncc)
        {
            ncc.CollectionChanged += (_, args) =>
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
        }

        AnimatePending(ic);
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

