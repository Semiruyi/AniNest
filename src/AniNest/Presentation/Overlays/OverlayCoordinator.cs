using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
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

        foreach (var child in _openOverlays.Where(candidate => ReferenceEquals(candidate.ParentOverlay, overlay)).ToArray())
        {
            child.ParentOverlay = null;
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
        var hit = AnalyzeHit(target);
        var keepSet = ComputeKeepSet(hit);
        var closeSet = _openOverlays
            .Where(overlay => overlay.IsOpen && overlay.CloseOnOutsideClick && !keepSet.Contains(overlay))
            .ToArray();

        if (closeSet.Length == 0)
            return;

        var behavior = ResolveBehavior(hit, e.ChangedButton, closeSet);
        var closeReason = ResolveCloseReason(hit);
        Log.Debug(
            $"OnPreviewMouseButtonDown: button={e.ChangedButton} hit={hit.Kind} " +
            $"primary={DescribeOverlay(hit.PrimaryOverlay)} keep={keepSet.Count} close={closeSet.Length} " +
            $"behavior={behavior} reason={closeReason}");

        if (behavior == OverlayPointerBehavior.CloseAndConsume)
        {
            e.Handled = true;
            SetConsumeFlag(e.ChangedButton, value: true);
        }

        foreach (var overlay in closeSet)
            overlay.Close(closeReason);
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

    private OverlayPointerBehavior ResolveBehavior(OverlayHitResult hit, MouseButton button, IReadOnlyCollection<AnimatedOverlay> closeSet)
    {
        switch (hit.Kind)
        {
            case OverlayHitKind.Anchor:
                foreach (var overlay in closeSet)
                {
                    if (hit.OverlayPath.Contains(overlay))
                        return overlay.GetAnchorClickBehavior(button);
                }

                return OverlayPointerBehavior.CloseAndPassThrough;

            case OverlayHitKind.Surface:
            case OverlayHitKind.ChildOverlay:
                return closeSet.Count > 0
                    ? OverlayPointerBehavior.CloseAndPassThrough
                    : OverlayPointerBehavior.KeepOpen;

            case OverlayHitKind.None:
            case OverlayHitKind.Outside:
            default:
                foreach (var overlay in closeSet)
                {
                    if (overlay.OutsidePointerBehavior == OverlayPointerBehavior.CloseAndConsume)
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
            return new OverlayHitResult { Kind = OverlayHitKind.Outside };

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

    private void SweepStaleState()
    {
        _openOverlays.RemoveWhere(static overlay => overlay is null || (!overlay.IsOpen && overlay.CurrentState == "Closed"));
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
}
