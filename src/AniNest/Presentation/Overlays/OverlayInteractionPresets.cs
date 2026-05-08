using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using AniNest.Features.Player.Settings;
using AniNest.Infrastructure.Logging;

namespace AniNest.Presentation.Overlays;

public static class OverlayInteractionPresets
{
    private static readonly Logger Log = AppLog.For(nameof(OverlayInteractionPresets));

    internal static void ApplyPreset(AnimatedOverlay overlay, OverlayInteractionPreset preset)
    {
        switch (preset)
        {
            case OverlayInteractionPreset.MenuLike:
                Apply(
                    overlay,
                    leftAnchor: OverlayPointerBehavior.CloseAndPassThrough,
                    rightAnchor: OverlayPointerBehavior.CloseAndPassThrough,
                    outside: OverlayPointerBehavior.CloseAndConsume,
                    passthroughTargets:
                        OverlayOutsidePassthroughTargets.TitleBarInteractive |
                        OverlayOutsidePassthroughTargets.TitleBarDragZone);
                break;
            case OverlayInteractionPreset.CaptureLike:
                Apply(
                    overlay,
                    leftAnchor: OverlayPointerBehavior.CloseAndPassThrough,
                    rightAnchor: OverlayPointerBehavior.CloseAndPassThrough,
                    outside: OverlayPointerBehavior.CloseAndConsume,
                    passthroughTargets:
                        OverlayOutsidePassthroughTargets.TitleBarInteractive |
                        OverlayOutsidePassthroughTargets.TitleBarDragZone);
                break;
            case OverlayInteractionPreset.ContextLike:
                Apply(
                    overlay,
                    leftAnchor: OverlayPointerBehavior.CloseAndConsume,
                    rightAnchor: OverlayPointerBehavior.CloseAndConsume,
                    outside: OverlayPointerBehavior.CloseAndConsume,
                    passthroughTargets: OverlayOutsidePassthroughTargets.None);
                break;
            case OverlayInteractionPreset.None:
            default:
                break;
        }
    }

    internal static OverlayPointerBehavior ResolveAnchorBehavior(AnimatedOverlay overlay, MouseButton button)
        => ResolveAnchorBehavior(overlay.InteractionPreset, button, overlay.LeftAnchorClickBehavior, overlay.RightAnchorClickBehavior);

    internal static OverlayPointerBehavior ResolveAnchorBehavior(
        OverlayInteractionPreset preset,
        MouseButton button,
        OverlayPointerBehavior leftAnchorBehavior,
        OverlayPointerBehavior rightAnchorBehavior)
    {
        return preset switch
        {
            OverlayInteractionPreset.MenuLike => OverlayPointerBehavior.CloseAndPassThrough,
            OverlayInteractionPreset.CaptureLike => OverlayPointerBehavior.CloseAndPassThrough,
            OverlayInteractionPreset.ContextLike => OverlayPointerBehavior.CloseAndConsume,
            _ => button == MouseButton.Right
                ? rightAnchorBehavior
                : leftAnchorBehavior,
        };
    }

    internal static OverlayPointerBehavior ResolveOutsideBehavior(AnimatedOverlay overlay, OverlayOutsideHitKind outsideKind)
        => ResolveOutsideBehavior(overlay.InteractionPreset, IsCaptureActive(overlay), outsideKind, overlay.OutsidePointerBehavior, overlay.OutsidePassthroughTargets);

    internal static OverlayPointerBehavior ResolveOutsideBehavior(
        OverlayInteractionPreset preset,
        bool isCaptureActive,
        OverlayOutsideHitKind outsideKind,
        OverlayPointerBehavior outsidePointerBehavior,
        OverlayOutsidePassthroughTargets outsidePassthroughTargets)
    {
        return preset switch
        {
            OverlayInteractionPreset.MenuLike => ResolveMenuLikeOutsideBehavior(outsideKind),
            OverlayInteractionPreset.CaptureLike => ResolveCaptureLikeOutsideBehavior(isCaptureActive, outsideKind),
            OverlayInteractionPreset.ContextLike => OverlayPointerBehavior.CloseAndConsume,
            _ => ResolvePropertyDrivenOutsideBehavior(outsideKind, outsidePointerBehavior, outsidePassthroughTargets),
        };
    }

    internal static bool ShouldCloseOnEscape(AnimatedOverlay overlay)
        => ShouldCloseOnEscape(overlay.InteractionPreset, IsCaptureActive(overlay), overlay.CloseOnEscape);

    internal static bool ShouldCloseOnEscape(
        OverlayInteractionPreset preset,
        bool isCaptureActive,
        bool closeOnEscape)
        => ResolveCloseRequest(preset, isCaptureActive, OverlayCloseReason.EscapeKey).ShouldClose &&
           (preset == OverlayInteractionPreset.CaptureLike || closeOnEscape);

    internal static OverlayCloseRequestDecision ResolveCloseRequest(AnimatedOverlay overlay, OverlayCloseReason reason)
    {
        var isCaptureActive = IsCaptureActive(overlay);
        Log.Debug(
            $"ResolveCloseRequest: overlay={overlay.Name} preset={overlay.InteractionPreset} " +
            $"reason={reason} isOpen={overlay.IsOpen} state={overlay.CurrentState} " +
            $"isCaptureActive={isCaptureActive}");

        var decision = ResolveCloseRequest(overlay.InteractionPreset, isCaptureActive, reason);
        Log.Debug(
            $"ResolveCloseRequest -> handled={decision.IsHandled} " +
            $"shouldClose={decision.ShouldClose} detail={decision.Detail}");
        return decision;
    }

    internal static OverlayCloseRequestDecision ResolveCloseRequest(
        OverlayInteractionPreset preset,
        bool isCaptureActive,
        OverlayCloseReason reason)
    {
        if (preset != OverlayInteractionPreset.CaptureLike)
        {
            return OverlayCloseRequestDecision.Close("non-capture-preset");
        }

        if (reason == OverlayCloseReason.EscapeKey)
        {
            return isCaptureActive
                ? OverlayCloseRequestDecision.Ignore("escape-reserved-for-capture")
                : OverlayCloseRequestDecision.Close("escape-close");
        }

        if (reason is not OverlayCloseReason.OutsideClick)
        {
            return OverlayCloseRequestDecision.Close("capture-non-outside-close");
        }

        return isCaptureActive
            ? OverlayCloseRequestDecision.Intercept("outside-click-canceled-active-capture")
            : OverlayCloseRequestDecision.Close("outside-close-no-active-capture");
    }

    internal static bool TryHandleCloseRequest(AnimatedOverlay overlay, OverlayCloseReason reason)
    {
        var decision = ResolveCloseRequest(overlay, reason);
        if (!decision.IsHandled)
            return false;

        if (overlay.InteractionPreset == OverlayInteractionPreset.CaptureLike &&
            reason == OverlayCloseReason.OutsideClick)
        {
            return TryCancelActiveCapture(overlay);
        }

        return true;
    }

    private static void Apply(
        AnimatedOverlay overlay,
        OverlayPointerBehavior leftAnchor,
        OverlayPointerBehavior rightAnchor,
        OverlayPointerBehavior outside,
        OverlayOutsidePassthroughTargets passthroughTargets)
    {
        overlay.CloseOnOutsideClick = true;
        overlay.CloseOnEscape = true;
        overlay.ResetAnchorOnClose = true;
        overlay.LeftAnchorClickBehavior = leftAnchor;
        overlay.RightAnchorClickBehavior = rightAnchor;
        overlay.OutsidePointerBehavior = outside;
        overlay.OutsidePassthroughTargets = passthroughTargets;
    }

    private static OverlayPointerBehavior ResolveMenuLikeOutsideBehavior(OverlayOutsideHitKind outsideKind)
    {
        return outsideKind switch
        {
            OverlayOutsideHitKind.TitleBarInteractive => OverlayPointerBehavior.CloseAndPassThrough,
            OverlayOutsideHitKind.TitleBarDragZone => OverlayPointerBehavior.CloseAndPassThrough,
            OverlayOutsideHitKind.ContentInteractive => OverlayPointerBehavior.CloseAndConsume,
            OverlayOutsideHitKind.ContentBackground => OverlayPointerBehavior.CloseAndConsume,
            _ => OverlayPointerBehavior.CloseAndConsume,
        };
    }

    private static OverlayPointerBehavior ResolveCaptureLikeOutsideBehavior(bool isCaptureActive, OverlayOutsideHitKind outsideKind)
    {
        Log.Debug(
            $"ResolveCaptureLikeOutsideBehavior: outsideKind={outsideKind} " +
            $"isCaptureActive={isCaptureActive}");

        if (isCaptureActive)
            return OverlayPointerBehavior.CloseAndConsume;

        return outsideKind switch
        {
            OverlayOutsideHitKind.TitleBarInteractive => OverlayPointerBehavior.CloseAndPassThrough,
            OverlayOutsideHitKind.TitleBarDragZone => OverlayPointerBehavior.CloseAndPassThrough,
            OverlayOutsideHitKind.ContentInteractive => OverlayPointerBehavior.CloseAndConsume,
            OverlayOutsideHitKind.ContentBackground => OverlayPointerBehavior.CloseAndConsume,
            _ => OverlayPointerBehavior.CloseAndConsume,
        };
    }

    private static OverlayPointerBehavior ResolvePropertyDrivenOutsideBehavior(
        OverlayOutsideHitKind outsideKind,
        OverlayPointerBehavior outsidePointerBehavior,
        OverlayOutsidePassthroughTargets outsidePassthroughTargets)
    {
        if (ShouldPassThroughOutsideHit(outsideKind, outsidePassthroughTargets))
            return OverlayPointerBehavior.CloseAndPassThrough;

        return outsidePointerBehavior;
    }

    private static bool ShouldPassThroughOutsideHit(OverlayOutsideHitKind outsideKind, OverlayOutsidePassthroughTargets outsidePassthroughTargets)
    {
        return outsideKind switch
        {
            OverlayOutsideHitKind.TitleBarInteractive =>
                outsidePassthroughTargets.HasFlag(OverlayOutsidePassthroughTargets.TitleBarInteractive),
            OverlayOutsideHitKind.TitleBarDragZone =>
                outsidePassthroughTargets.HasFlag(OverlayOutsidePassthroughTargets.TitleBarDragZone),
            OverlayOutsideHitKind.ContentInteractive =>
                outsidePassthroughTargets.HasFlag(OverlayOutsidePassthroughTargets.ContentInteractive),
            OverlayOutsideHitKind.ContentBackground =>
                outsidePassthroughTargets.HasFlag(OverlayOutsidePassthroughTargets.ContentBackground),
            _ => false,
        };
    }

    private static bool IsCaptureActive(AnimatedOverlay overlay)
    {
        var settings = TryGetPlayerInputSettings(overlay);
        var isCapturing = settings is { IsCapturing: true };
        Log.Debug(
            $"IsCaptureActive: overlay={overlay.Name} foundSettings={settings != null} " +
            $"isCapturing={isCapturing}");
        return isCapturing;
    }

    private static bool TryCancelActiveCapture(AnimatedOverlay overlay)
    {
        var settings = TryGetPlayerInputSettings(overlay);
        if (settings is not { IsCapturing: true })
        {
            Log.Debug(
                $"TryCancelActiveCapture: overlay={overlay.Name} foundSettings={settings != null} " +
                $"isCapturing={settings?.IsCapturing.ToString() ?? "null"}");
            return false;
        }

        Log.Debug($"TryCancelActiveCapture: overlay={overlay.Name} cancel active capture");
        settings.CancelCapture();
        return true;
    }

    private static PlayerInputSettingsViewModel? TryGetPlayerInputSettings(AnimatedOverlay overlay)
    {
        if (overlay.Content is not DependencyObject root)
        {
            Log.Debug($"TryGetPlayerInputSettings: overlay={overlay.Name} content is not DependencyObject");
            return null;
        }

        var settings = FindPlayerInputSettings(root);
        Log.Debug(
            $"TryGetPlayerInputSettings: overlay={overlay.Name} contentType={root.GetType().Name} " +
            $"foundSettings={settings != null}");
        return settings;
    }

    private static PlayerInputSettingsViewModel? FindPlayerInputSettings(DependencyObject root)
    {
        if (root is FrameworkElement { DataContext: PlayerInputSettingsViewModel settings })
            return settings;

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var result = FindPlayerInputSettings(VisualTreeHelper.GetChild(root, i));
            if (result != null)
                return result;
        }

        return null;
    }
}
