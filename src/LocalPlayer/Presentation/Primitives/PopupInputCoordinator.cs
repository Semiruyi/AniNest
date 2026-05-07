using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LocalPlayer.Presentation.Primitives;

public enum PopupHitKind
{
    None,
    PopupChild,
    PlacementTarget,
    TitleBarInteractive,
    TitleBarDragZone,
    VideoSurface,
    ControlBarInteractive,
    ControlBarGesture,
    DismissBackground,
}

public enum PopupInputAction
{
    KeepAndHandle,
    CloseAndHandle,
    CloseAndConsume,
}

[Flags]
public enum PopupPassthroughTargets
{
    None = 0,
    VideoSurface = 1 << 0,
    ControlBarGesture = 1 << 1,
    DismissBackground = 1 << 2,
}

public enum PopupCloseReason
{
    Programmatic,
    OutsideClick,
    ChainSwitch,
    TargetToggle,
}

public sealed class PopupHitResult
{
    public PopupHitKind Kind { get; init; }
    public DependencyObject? OriginalTarget { get; init; }
    public IReadOnlyList<AnimatedPopup> PopupPath { get; init; } = Array.Empty<AnimatedPopup>();
    public PopupInputAction Action { get; init; }
}

/// <summary>
/// Coordinates popup dismissal and click routing across the app.
/// </summary>
public sealed class PopupInputCoordinator
{
    private static readonly Lazy<PopupInputCoordinator> _instance = new(() => new PopupInputCoordinator());

    private readonly HashSet<AnimatedPopup> _openPopups = new();
    private readonly Dictionary<UIElement, AnimatedPopup> _popupRoots = new();
    private readonly Dictionary<PopupHitKind, HashSet<DependencyObject>> _registeredRegions = new()
    {
        [PopupHitKind.TitleBarInteractive] = new(),
        [PopupHitKind.TitleBarDragZone] = new(),
        [PopupHitKind.VideoSurface] = new(),
        [PopupHitKind.ControlBarInteractive] = new(),
        [PopupHitKind.ControlBarGesture] = new(),
        [PopupHitKind.DismissBackground] = new(),
    };
    private readonly HashSet<Window> _attachedWindows = new();
    private bool _consumeCurrentLeftClickSequence;

    public static PopupInputCoordinator Instance => _instance.Value;

    private PopupInputCoordinator()
    {
    }

    public void Attach(Window window)
    {
        if (!_attachedWindows.Add(window))
            return;

        window.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        window.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
    }

    public void RegisterRegion(DependencyObject element, PopupHitKind kind)
    {
        if (!_registeredRegions.TryGetValue(kind, out var regions))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported popup hit region.");

        regions.Add(element);
    }

    public void UnregisterRegion(DependencyObject element, PopupHitKind kind)
    {
        if (_registeredRegions.TryGetValue(kind, out var regions))
            regions.Remove(element);
    }

    public void RegisterOpenedPopup(AnimatedPopup popup)
    {
        SweepStaleState();
        _openPopups.Add(popup);

        if (popup.Child is UIElement child)
        {
            var root = PresentationSource.FromDependencyObject(child)?.RootVisual;
            if (root is UIElement rootElement)
                _popupRoots[rootElement] = popup;
        }

        popup.ParentPopup = FindParentPopup(popup);
    }

    public void RegisterClosedPopup(AnimatedPopup popup)
    {
        _openPopups.Remove(popup);

        var staleRoots = _popupRoots
            .Where(entry => entry.Value == popup)
            .Select(entry => entry.Key)
            .ToList();

        foreach (var root in staleRoots)
            _popupRoots.Remove(root);

        popup.ParentPopup = null;
        SweepStaleState();
    }

    private void SweepStaleState()
    {
        _openPopups.RemoveWhere(IsPopupStale);

        var staleRoots = _popupRoots
            .Where(entry => entry.Key is null || entry.Value is null || IsPopupStale(entry.Value))
            .Select(entry => entry.Key)
            .ToList();

        foreach (var root in staleRoots)
            _popupRoots.Remove(root);

        foreach (var regions in _registeredRegions.Values)
        {
            regions.RemoveWhere(static element => element is null || !IsElementAlive(element));
        }
    }

    private static bool IsPopupStale(AnimatedPopup popup)
    {
        return popup is null || (!popup.IsOpen && !popup.IsOpenAnimated && !IsElementAlive(popup));
    }

    private static bool IsElementAlive(DependencyObject element)
    {
        return element switch
        {
            FrameworkElement frameworkElement => frameworkElement.IsLoaded || PresentationSource.FromDependencyObject(frameworkElement) != null,
            Visual visual => PresentationSource.FromVisual(visual) != null,
            System.Windows.Media.Media3D.Visual3D visual3D => PresentationSource.FromDependencyObject(visual3D) != null,
            _ => true,
        };
    }

    public AnimatedPopup? FindParentPopup(AnimatedPopup popup)
    {
        if (popup.PlacementTarget is not DependencyObject target)
            return null;

        DependencyObject? current = target;
        while (current != null)
        {
            foreach (var candidate in _openPopups)
            {
                if (candidate == popup)
                    continue;

                if (candidate.Child is UIElement child && child.IsAncestorOf(current))
                    return candidate;
            }

            current = GetVisualOrLogicalParent(current);
        }

        return null;
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _consumeCurrentLeftClickSequence = false;

        if (_openPopups.Count == 0)
            return;

        var hit = AnalyzeHit(e.OriginalSource as DependencyObject);
        var keepSet = ComputeKeepSet(hit);
        var closeSet = _openPopups
            .Where(popup => popup.CloseOnOutsideClick && popup.IsOpenAnimated && !keepSet.Contains(popup))
            .ToArray();

        if (closeSet.Length == 0)
            return;

        var effectiveAction = GetEffectiveAction(hit, closeSet);
        if (effectiveAction == PopupInputAction.CloseAndConsume)
        {
            e.Handled = true;
            _consumeCurrentLeftClickSequence = true;
        }

        var closeReason = hit.Kind switch
        {
            PopupHitKind.PopupChild => PopupCloseReason.ChainSwitch,
            PopupHitKind.PlacementTarget => PopupCloseReason.TargetToggle,
            _ => PopupCloseReason.OutsideClick,
        };

        Dispatcher.CurrentDispatcher.BeginInvoke(
            new Action(() => ClosePopups(closeSet, closeReason)),
            DispatcherPriority.Background);
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_consumeCurrentLeftClickSequence)
            return;

        e.Handled = true;
        _consumeCurrentLeftClickSequence = false;
    }

    private static PopupInputAction GetEffectiveAction(PopupHitResult hit, IReadOnlyCollection<AnimatedPopup> closeSet)
    {
        if (hit.Action != PopupInputAction.CloseAndConsume)
            return hit.Action;

        foreach (var popup in closeSet)
        {
            if (!popup.AllowsPassthrough(hit.Kind))
                return PopupInputAction.CloseAndConsume;
        }

        return PopupInputAction.CloseAndHandle;
    }

    private void ClosePopups(IEnumerable<AnimatedPopup> popups, PopupCloseReason reason)
    {
        foreach (var popup in popups)
            popup.RequestClose(reason);
    }

    private PopupHitResult AnalyzeHit(DependencyObject? target)
    {
        if (target == null)
        {
            return new PopupHitResult
            {
                Kind = PopupHitKind.None,
                Action = PopupInputAction.CloseAndConsume,
            };
        }

        if (TryGetPopupPathFromPopupChild(target, out var popupPath))
        {
            return new PopupHitResult
            {
                Kind = PopupHitKind.PopupChild,
                OriginalTarget = target,
                PopupPath = popupPath,
                Action = PopupInputAction.KeepAndHandle,
            };
        }

        if (TryGetPopupPathFromPlacementTarget(target, out popupPath))
        {
            return new PopupHitResult
            {
                Kind = PopupHitKind.PlacementTarget,
                OriginalTarget = target,
                PopupPath = popupPath,
                Action = PopupInputAction.CloseAndHandle,
            };
        }

        if (IsInsideRegisteredRegion(target, PopupHitKind.TitleBarInteractive))
        {
            return new PopupHitResult
            {
                Kind = PopupHitKind.TitleBarInteractive,
                OriginalTarget = target,
                Action = PopupInputAction.CloseAndHandle,
            };
        }

        if (IsInsideRegisteredRegion(target, PopupHitKind.TitleBarDragZone))
        {
            return new PopupHitResult
            {
                Kind = PopupHitKind.TitleBarDragZone,
                OriginalTarget = target,
                Action = PopupInputAction.CloseAndHandle,
            };
        }

        if (IsInsideRegisteredRegion(target, PopupHitKind.ControlBarInteractive))
        {
            return new PopupHitResult
            {
                Kind = PopupHitKind.ControlBarInteractive,
                OriginalTarget = target,
                Action = PopupInputAction.CloseAndHandle,
            };
        }

        if (IsInsideRegisteredRegion(target, PopupHitKind.VideoSurface))
        {
            return new PopupHitResult
            {
                Kind = PopupHitKind.VideoSurface,
                OriginalTarget = target,
                Action = PopupInputAction.CloseAndConsume,
            };
        }

        if (IsInsideRegisteredRegion(target, PopupHitKind.ControlBarGesture))
        {
            return new PopupHitResult
            {
                Kind = PopupHitKind.ControlBarGesture,
                OriginalTarget = target,
                Action = PopupInputAction.CloseAndConsume,
            };
        }

        if (IsInsideRegisteredRegion(target, PopupHitKind.DismissBackground))
        {
            return new PopupHitResult
            {
                Kind = PopupHitKind.DismissBackground,
                OriginalTarget = target,
                Action = PopupInputAction.CloseAndConsume,
            };
        }

        return new PopupHitResult
        {
            Kind = PopupHitKind.DismissBackground,
            OriginalTarget = target,
            Action = PopupInputAction.CloseAndConsume,
        };
    }

    private HashSet<AnimatedPopup> ComputeKeepSet(PopupHitResult hit)
    {
        var keepSet = new HashSet<AnimatedPopup>();
        foreach (var popup in hit.PopupPath)
            keepSet.Add(popup);

        return keepSet;
    }

    private bool TryGetPopupPathFromPopupChild(DependencyObject target, out IReadOnlyList<AnimatedPopup> popupPath)
    {
        popupPath = Array.Empty<AnimatedPopup>();

        var source = PresentationSource.FromDependencyObject(target);
        if (source?.RootVisual is UIElement root &&
            _popupRoots.TryGetValue(root, out var rootPopup))
        {
            popupPath = BuildPopupPath(rootPopup);
            return true;
        }

        foreach (var popup in _openPopups)
        {
            if (popup.Child is UIElement child && child.IsAncestorOf(target))
            {
                popupPath = BuildPopupPath(popup);
                return true;
            }
        }

        return false;
    }

    private bool TryGetPopupPathFromPlacementTarget(DependencyObject target, out IReadOnlyList<AnimatedPopup> popupPath)
    {
        popupPath = Array.Empty<AnimatedPopup>();

        AnimatedPopup? bestMatch = null;
        var bestDepth = -1;

        foreach (var popup in _openPopups)
        {
            if (popup.PlacementTarget is not DependencyObject placementTarget)
                continue;

            if (!IsAncestorOrSelf(placementTarget, target))
                continue;

            var depth = GetPopupDepth(popup);
            if (depth > bestDepth)
            {
                bestDepth = depth;
                bestMatch = popup;
            }
        }

        if (bestMatch == null)
            return false;

        popupPath = BuildPopupPath(bestMatch);
        return true;
    }

    private bool IsInsideRegisteredRegion(DependencyObject target, PopupHitKind kind)
    {
        if (!_registeredRegions.TryGetValue(kind, out var regions))
            return false;

        foreach (var root in regions)
        {
            if (IsAncestorOrSelf(root, target))
                return true;
        }

        return false;
    }

    private static IReadOnlyList<AnimatedPopup> BuildPopupPath(AnimatedPopup popup)
    {
        var path = new List<AnimatedPopup>();
        AnimatedPopup? current = popup;
        while (current != null)
        {
            path.Add(current);
            current = current.ParentPopup;
        }

        return path;
    }

    private static int GetPopupDepth(AnimatedPopup popup)
    {
        var depth = 0;
        var current = popup.ParentPopup;
        while (current != null)
        {
            depth++;
            current = current.ParentPopup;
        }

        return depth;
    }

    private static bool IsAncestorOrSelf(DependencyObject ancestor, DependencyObject target)
    {
        DependencyObject? current = target;
        while (current != null)
        {
            if (current == ancestor)
                return true;

            current = GetVisualOrLogicalParent(current);
        }

        return false;
    }

    private static DependencyObject? GetVisualOrLogicalParent(DependencyObject current)
    {
        if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
            return VisualTreeHelper.GetParent(current);

        return LogicalTreeHelper.GetParent(current);
    }
}

