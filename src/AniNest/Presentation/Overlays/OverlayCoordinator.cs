using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AniNest.Infrastructure.Logging;

namespace AniNest.Presentation.Overlays;

/// <summary>
/// Coordinates pointer-based overlay dismissal across a window.
///
/// Current behavior contract:
/// - The deepest matching overlay becomes the primary hit target.
/// - Its parent chain is preserved via <see cref="OverlayHitResult.OverlayPath"/>.
/// - Pointer hits on an anchor use the anchor-specific pointer behavior.
/// - Pointer hits inside a surface/child chain keep that chain alive while allowing
///   sibling branches to close via <see cref="OverlayCloseReason.ChainSwitch"/>.
/// - Pointer hits outside every open overlay close eligible overlays via
///   <see cref="OverlayCloseReason.OutsideClick"/>.
/// </summary>
public sealed class OverlayCoordinator
{
    private static readonly Lazy<OverlayCoordinator> _instance = new(() => new OverlayCoordinator());
    private static readonly Logger Log = AppLog.For(nameof(OverlayCoordinator));
    private readonly HashSet<AnimatedOverlay> _openOverlays = new();
    private readonly HashSet<Window> _attachedWindows = new();
    private readonly Dictionary<OverlayOutsideHitKind, HashSet<DependencyObject>> _registeredRegions = new()
    {
        [OverlayOutsideHitKind.TitleBarInteractive] = new(),
        [OverlayOutsideHitKind.TitleBarDragZone] = new(),
        [OverlayOutsideHitKind.ContentInteractive] = new(),
        [OverlayOutsideHitKind.ContentBackground] = new(),
    };
    private bool _consumeCurrentLeftClickSequence;
    private bool _consumeCurrentRightClickSequence;

    public static OverlayCoordinator Instance => _instance.Value;

    private OverlayCoordinator()
    {
    }

    public void Attach(Window window)
    {
        if (!_attachedWindows.Add(window))
            return;

        window.PreviewMouseLeftButtonDown += OnPreviewMouseButtonDown;
        window.PreviewMouseRightButtonDown += OnPreviewMouseButtonDown;
        window.PreviewMouseLeftButtonUp += OnPreviewMouseButtonUp;
        window.PreviewMouseRightButtonUp += OnPreviewMouseButtonUp;
    }

    public void RegisterRegion(DependencyObject element, OverlayOutsideHitKind kind)
    {
        if (!_registeredRegions.TryGetValue(kind, out var regions))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported overlay hit region.");

        regions.Add(element);
    }

    public void RegisterOpenedOverlay(AnimatedOverlay overlay)
    {
        SweepStaleState();
        _openOverlays.Add(overlay);
        overlay.ParentOverlay = FindParentOverlay(overlay);
        if (Window.GetWindow(overlay) is Window window)
            Attach(window);
    }

    public void RegisterClosedOverlay(AnimatedOverlay overlay)
    {
        _openOverlays.Remove(overlay);

        var closeDescendants = OverlayInteractionPresets.ShouldCloseDescendantsOnParentClose(overlay.InteractionPreset);
        foreach (var child in _openOverlays.Where(candidate => ReferenceEquals(candidate.ParentOverlay, overlay)).ToArray())
        {
            child.ParentOverlay = null;
            if (closeDescendants)
                child.Close(OverlayCloseReason.ParentClosed);
        }

        overlay.ParentOverlay = null;
        SweepStaleState();
    }

    private void OnPreviewMouseButtonDown(object sender, MouseButtonEventArgs e)
    {
        ResetConsumeFlag(e.ChangedButton);
        SweepStaleState();
        if (_openOverlays.Count == 0)
            return;

        var target = e.OriginalSource as DependencyObject;
        var decision = BuildPointerDecision(target, e.ChangedButton);
        if (decision == null || decision.CloseSet.Count == 0)
            return;

        Log.Debug(
            $"OnPreviewMouseButtonDown: button={e.ChangedButton} hit={decision.Hit.Kind} " +
            $"outsideKind={decision.Hit.OutsideKind} primary={DescribeOverlay(decision.Hit.PrimaryOverlay)} " +
            $"keep={decision.KeepSet.Count} interceptedKeep={decision.InterceptedKeepSet.Count} close={decision.CloseSet.Count} " +
            $"behavior={decision.PointerBehavior} reason={decision.CloseReason}");

        if (decision.PointerBehavior == OverlayPointerBehavior.CloseAndConsume)
        {
            e.Handled = true;
            SetConsumeFlag(e.ChangedButton, value: true);
        }

        foreach (var overlay in decision.CloseSet)
            overlay.Close(decision.CloseReason);
    }

    private void OnPreviewMouseButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!GetConsumeFlag(e.ChangedButton))
            return;

        e.Handled = true;
        ResetConsumeFlag(e.ChangedButton);
    }

    private AnimatedOverlay? FindParentOverlay(AnimatedOverlay overlay)
    {
        if (overlay.AnchorElement is not DependencyObject target)
            return null;

        DependencyObject? current = target;
        while (current != null)
        {
            foreach (var candidate in _openOverlays)
            {
                if (candidate == overlay)
                    continue;

                if (candidate.ContainsSurfaceTarget(current))
                    return candidate;
            }

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                System.Windows.Media.Media3D.Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                _ => LogicalTreeHelper.GetParent(current),
            };
        }

        return null;
    }

    private HashSet<AnimatedOverlay> ComputeKeepSet(OverlayHitResult hit)
    {
        var keep = new HashSet<AnimatedOverlay>();
        if (hit.OverlayPath.Count == 0)
            return keep;

        foreach (var overlay in hit.OverlayPath)
        {
            keep.Add(overlay);
        }

        return keep;
    }

    private OverlayPointerDecision? BuildPointerDecision(DependencyObject? target, MouseButton button)
    {
        var hit = AnalyzeHit(target);
        var keepSet = ComputeKeepSet(hit);
        keepSet = ExpandKeepSetForChainPolicy(hit, keepSet);
        var initialCloseSet = _openOverlays
            .Where(overlay => overlay.IsOpen && overlay.CloseOnOutsideClick && !keepSet.Contains(overlay))
            .ToArray();

        if (initialCloseSet.Length == 0)
            return null;

        var closeReason = ResolveCloseReason(hit);
        if (closeReason == OverlayCloseReason.ChainSwitch &&
            hit.PrimaryOverlay != null &&
            !OverlayInteractionPresets.ShouldCloseSiblingBranchesOnChainInteraction(hit.PrimaryOverlay.InteractionPreset))
        {
            initialCloseSet = [];
        }

        if (initialCloseSet.Length == 0)
            return null;

        var interceptedKeepSet = ComputeInterceptedKeepSet(initialCloseSet, closeReason);
        var closeSet = interceptedKeepSet.Count > 0
            ? initialCloseSet.Where(overlay => !interceptedKeepSet.Contains(overlay)).ToArray()
            : initialCloseSet;

        return new OverlayPointerDecision
        {
            Hit = hit,
            CloseReason = closeReason,
            PointerBehavior = ResolveBehavior(hit, button, closeSet),
            KeepSet = keepSet,
            InterceptedKeepSet = interceptedKeepSet,
            CloseSet = closeSet,
        };
    }

    private OverlayPointerBehavior ResolveBehavior(OverlayHitResult hit, MouseButton button, IReadOnlyCollection<AnimatedOverlay> closeSet)
    {
        switch (hit.Kind)
        {
            case OverlayHitKind.Anchor:
                foreach (var overlay in closeSet)
                {
                    if (hit.OverlayPath.Contains(overlay))
                        return OverlayInteractionPresets.ResolveAnchorBehavior(overlay, button);
                }

                return OverlayPointerBehavior.CloseAndPassThrough;

            case OverlayHitKind.Surface:
            case OverlayHitKind.ChildOverlay:
                return OverlayInteractionPresets.ResolveChainBehavior(
                    hit.PrimaryOverlay?.InteractionPreset ?? OverlayInteractionPreset.None,
                    hit.Kind,
                    closeSet.Count > 0);

            case OverlayHitKind.None:
            case OverlayHitKind.Outside:
            default:
                foreach (var overlay in closeSet)
                {
                    if (OverlayInteractionPresets.ResolveOutsideBehavior(overlay, hit.OutsideKind) == OverlayPointerBehavior.CloseAndConsume)
                        return OverlayPointerBehavior.CloseAndConsume;
                }

                return OverlayPointerBehavior.CloseAndPassThrough;
        }
    }

    private OverlayHitResult AnalyzeHit(DependencyObject? target)
    {
        if (target == null)
            return new OverlayHitResult { Kind = OverlayHitKind.None };

        var direct = _openOverlays
            .Where(overlay => overlay.IsOpen && (overlay.ContainsSurfaceTarget(target) || overlay.ContainsAnchorTarget(target)))
            .ToArray();

        if (direct.Length == 0)
        {
            return new OverlayHitResult
            {
                Kind = OverlayHitKind.Outside,
                OutsideKind = ResolveOutsideHitKind(target),
            };
        }

        var primary = direct
            .OrderByDescending(GetOverlayDepth)
            .First();

        var hitSurface = primary.ContainsSurfaceTarget(target);
        var path = BuildOverlayPath(primary);
        var kind = hitSurface
            ? (path.Count > 1 ? OverlayHitKind.ChildOverlay : OverlayHitKind.Surface)
            : OverlayHitKind.Anchor;

        return new OverlayHitResult
        {
            Kind = kind,
            PrimaryOverlay = primary,
            OverlayPath = path,
        };
    }

    private static OverlayCloseReason ResolveCloseReason(OverlayHitResult hit)
        => hit.Kind switch
        {
            OverlayHitKind.Anchor => OverlayCloseReason.Toggle,
            OverlayHitKind.Surface => OverlayCloseReason.ChainSwitch,
            OverlayHitKind.ChildOverlay => OverlayCloseReason.ChainSwitch,
            _ => OverlayCloseReason.OutsideClick,
        };

    private static List<AnimatedOverlay> BuildOverlayPath(AnimatedOverlay overlay)
    {
        var path = new List<AnimatedOverlay>();
        var current = overlay;
        while (current != null)
        {
            path.Add(current);
            current = current.ParentOverlay;
        }

        return path;
    }

    private static int GetOverlayDepth(AnimatedOverlay overlay)
    {
        int depth = 0;
        var current = overlay.ParentOverlay;
        while (current != null)
        {
            depth++;
            current = current.ParentOverlay;
        }

        return depth;
    }

    private HashSet<AnimatedOverlay> ExpandKeepSetForChainPolicy(
        OverlayHitResult hit,
        HashSet<AnimatedOverlay> keepSet)
    {
        if (hit.Kind != OverlayHitKind.Surface || hit.PrimaryOverlay == null)
            return keepSet;

        if (OverlayInteractionPresets.ShouldCloseDescendantsOnAncestorSurfaceHit(hit.PrimaryOverlay.InteractionPreset))
            return keepSet;

        foreach (var descendant in EnumerateDescendants(hit.PrimaryOverlay))
            keepSet.Add(descendant);

        return keepSet;
    }

    private IEnumerable<AnimatedOverlay> EnumerateDescendants(AnimatedOverlay overlay)
    {
        foreach (var candidate in _openOverlays)
        {
            var current = candidate.ParentOverlay;
            while (current != null)
            {
                if (ReferenceEquals(current, overlay))
                {
                    yield return candidate;
                    break;
                }

                current = current.ParentOverlay;
            }
        }
    }

    private static HashSet<AnimatedOverlay> ComputeInterceptedKeepSet(
        IReadOnlyCollection<AnimatedOverlay> closeSet,
        OverlayCloseReason reason)
    {
        var keep = new HashSet<AnimatedOverlay>();

        foreach (var overlay in closeSet.OrderByDescending(GetOverlayDepth))
        {
            var decision = OverlayInteractionPresets.ResolveCloseRequest(overlay, reason);
            if (!decision.IsHandled || decision.ShouldClose)
                continue;

            OverlayInteractionPresets.TryHandleCloseRequest(overlay, reason);

            var current = overlay;
            var keepAncestors = OverlayInteractionPresets.ShouldKeepAncestorChainWhenChildInterceptsClose(overlay.InteractionPreset);
            while (current != null)
            {
                keep.Add(current);
                if (!keepAncestors)
                    break;

                current = current.ParentOverlay;
            }
        }

        return keep;
    }

    private void SweepStaleState()
    {
        _openOverlays.RemoveWhere(static overlay => overlay is null || (!overlay.IsOpen && overlay.CurrentState == "Closed"));

        foreach (var regions in _registeredRegions.Values)
            regions.RemoveWhere(static element => element is null || !IsElementAlive(element));
    }

    private static string DescribeOverlay(AnimatedOverlay? overlay)
    {
        if (overlay == null)
            return "null";

        return overlay.Name;
    }

    private bool GetConsumeFlag(MouseButton button)
        => button switch
        {
            MouseButton.Left => _consumeCurrentLeftClickSequence,
            MouseButton.Right => _consumeCurrentRightClickSequence,
            _ => false,
        };

    private void SetConsumeFlag(MouseButton button, bool value)
    {
        switch (button)
        {
            case MouseButton.Left:
                _consumeCurrentLeftClickSequence = value;
                break;
            case MouseButton.Right:
                _consumeCurrentRightClickSequence = value;
                break;
        }
    }

    private void ResetConsumeFlag(MouseButton button) => SetConsumeFlag(button, value: false);

    private OverlayOutsideHitKind ResolveOutsideHitKind(DependencyObject? target)
    {
        if (target == null)
            return OverlayOutsideHitKind.None;

        if (IsInsideRegisteredRegion(target, OverlayOutsideHitKind.TitleBarInteractive))
            return OverlayOutsideHitKind.TitleBarInteractive;

        if (IsInsideRegisteredRegion(target, OverlayOutsideHitKind.TitleBarDragZone))
            return OverlayOutsideHitKind.TitleBarDragZone;

        if (IsInsideRegisteredRegion(target, OverlayOutsideHitKind.ContentInteractive))
            return OverlayOutsideHitKind.ContentInteractive;

        if (IsInsideRegisteredRegion(target, OverlayOutsideHitKind.ContentBackground))
            return OverlayOutsideHitKind.ContentBackground;

        if (IsInsideLikelyInteractiveElement(target))
            return OverlayOutsideHitKind.ContentInteractive;

        return OverlayOutsideHitKind.ContentBackground;
    }

    private bool IsInsideRegisteredRegion(DependencyObject target, OverlayOutsideHitKind kind)
    {
        if (!_registeredRegions.TryGetValue(kind, out var regions) || regions.Count == 0)
            return false;

        DependencyObject? current = target;
        while (current != null)
        {
            if (regions.Contains(current))
                return true;

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                System.Windows.Media.Media3D.Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                _ => LogicalTreeHelper.GetParent(current),
            };
        }

        return false;
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

    private static bool IsInsideLikelyInteractiveElement(DependencyObject target)
    {
        DependencyObject? current = target;
        while (current != null)
        {
            if (current is ButtonBase or TextBoxBase or PasswordBox or ComboBox or Slider or ScrollBar or Thumb)
                return true;

            if (current is ListBoxItem or TreeViewItem or TabItem)
                return true;

            if (current is FrameworkElement frameworkElement)
            {
                if (frameworkElement.Cursor != null && frameworkElement.Cursor != Cursors.Arrow)
                    return true;

                if (frameworkElement is Control control && control.Focusable)
                    return true;
            }

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                System.Windows.Media.Media3D.Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                _ => LogicalTreeHelper.GetParent(current),
            };
        }

        return false;
    }
}
