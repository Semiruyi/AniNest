using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using AniNest.Features.Player.Settings;
using AniNest.Infrastructure.Logging;

namespace AniNest.Presentation.Overlays;

public static class OverlayInteractionPresets
{
    private static readonly Logger Log = AppLog.For(nameof(OverlayInteractionPresets));
    private static readonly OverlayInteractionProfile MenuLikeProfile = new(
        LeftAnchorBehavior: OverlayPointerBehavior.CloseAndPassThrough,
        RightAnchorBehavior: OverlayPointerBehavior.CloseAndPassThrough,
        SurfaceBehaviorWhenClosingOthers: OverlayPointerBehavior.CloseAndPassThrough,
        SurfaceBehaviorWhenStable: OverlayPointerBehavior.KeepOpen,
        ChildOverlayBehaviorWhenClosingOthers: OverlayPointerBehavior.CloseAndPassThrough,
        ChildOverlayBehaviorWhenStable: OverlayPointerBehavior.KeepOpen,
        TitleBarInteractiveOutsideBehavior: OverlayPointerBehavior.CloseAndPassThrough,
        TitleBarDragZoneOutsideBehavior: OverlayPointerBehavior.CloseAndPassThrough,
        ContentInteractiveOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        ContentBackgroundOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        RightButtonTitleBarInteractiveOutsideBehavior: null,
        RightButtonTitleBarDragZoneOutsideBehavior: null,
        RightButtonContentInteractiveOutsideBehavior: null,
        RightButtonContentBackgroundOutsideBehavior: null,
        DefaultOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        OutsidePassthroughTargets:
            OverlayOutsidePassthroughTargets.TitleBarInteractive |
            OverlayOutsidePassthroughTargets.TitleBarDragZone,
        CloseOnOutsideClick: true,
        CloseOnEscape: true,
        ResetAnchorOnClose: true,
        EscapeReservedWhileCapturing: false,
        KeepAncestorChainWhenChildInterceptsClose: true,
        CloseDescendantsOnParentClose: true,
        CloseDescendantsOnAncestorSurfaceHit: true,
        CloseSiblingBranchesOnChainInteraction: true);
    private static readonly OverlayInteractionProfile CaptureLikeProfile = new(
        LeftAnchorBehavior: OverlayPointerBehavior.CloseAndPassThrough,
        RightAnchorBehavior: OverlayPointerBehavior.CloseAndPassThrough,
        SurfaceBehaviorWhenClosingOthers: OverlayPointerBehavior.CloseAndPassThrough,
        SurfaceBehaviorWhenStable: OverlayPointerBehavior.KeepOpen,
        ChildOverlayBehaviorWhenClosingOthers: OverlayPointerBehavior.CloseAndPassThrough,
        ChildOverlayBehaviorWhenStable: OverlayPointerBehavior.KeepOpen,
        TitleBarInteractiveOutsideBehavior: OverlayPointerBehavior.CloseAndPassThrough,
        TitleBarDragZoneOutsideBehavior: OverlayPointerBehavior.CloseAndPassThrough,
        ContentInteractiveOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        ContentBackgroundOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        RightButtonTitleBarInteractiveOutsideBehavior: null,
        RightButtonTitleBarDragZoneOutsideBehavior: null,
        RightButtonContentInteractiveOutsideBehavior: null,
        RightButtonContentBackgroundOutsideBehavior: null,
        DefaultOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        OutsidePassthroughTargets:
            OverlayOutsidePassthroughTargets.TitleBarInteractive |
            OverlayOutsidePassthroughTargets.TitleBarDragZone,
        CloseOnOutsideClick: true,
        CloseOnEscape: true,
        ResetAnchorOnClose: true,
        EscapeReservedWhileCapturing: true,
        KeepAncestorChainWhenChildInterceptsClose: true,
        CloseDescendantsOnParentClose: true,
        CloseDescendantsOnAncestorSurfaceHit: true,
        CloseSiblingBranchesOnChainInteraction: true);
    private static readonly OverlayInteractionProfile ContextLikeProfile = new(
        LeftAnchorBehavior: OverlayPointerBehavior.CloseAndConsume,
        RightAnchorBehavior: OverlayPointerBehavior.CloseAndConsume,
        SurfaceBehaviorWhenClosingOthers: OverlayPointerBehavior.CloseAndPassThrough,
        SurfaceBehaviorWhenStable: OverlayPointerBehavior.KeepOpen,
        ChildOverlayBehaviorWhenClosingOthers: OverlayPointerBehavior.CloseAndPassThrough,
        ChildOverlayBehaviorWhenStable: OverlayPointerBehavior.KeepOpen,
        TitleBarInteractiveOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        TitleBarDragZoneOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        ContentInteractiveOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        ContentBackgroundOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        RightButtonTitleBarInteractiveOutsideBehavior: null,
        RightButtonTitleBarDragZoneOutsideBehavior: null,
        RightButtonContentInteractiveOutsideBehavior: null,
        RightButtonContentBackgroundOutsideBehavior: null,
        DefaultOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        OutsidePassthroughTargets: OverlayOutsidePassthroughTargets.None,
        CloseOnOutsideClick: true,
        CloseOnEscape: true,
        ResetAnchorOnClose: true,
        EscapeReservedWhileCapturing: false,
        KeepAncestorChainWhenChildInterceptsClose: true,
        CloseDescendantsOnParentClose: true,
        CloseDescendantsOnAncestorSurfaceHit: true,
        CloseSiblingBranchesOnChainInteraction: true);
    private static readonly OverlayInteractionProfile CardContextLikeProfile = new(
        LeftAnchorBehavior: OverlayPointerBehavior.CloseAndConsume,
        RightAnchorBehavior: OverlayPointerBehavior.CloseAndConsume,
        SurfaceBehaviorWhenClosingOthers: OverlayPointerBehavior.CloseAndPassThrough,
        SurfaceBehaviorWhenStable: OverlayPointerBehavior.KeepOpen,
        ChildOverlayBehaviorWhenClosingOthers: OverlayPointerBehavior.CloseAndPassThrough,
        ChildOverlayBehaviorWhenStable: OverlayPointerBehavior.KeepOpen,
        TitleBarInteractiveOutsideBehavior: OverlayPointerBehavior.CloseAndPassThrough,
        TitleBarDragZoneOutsideBehavior: OverlayPointerBehavior.CloseAndPassThrough,
        ContentInteractiveOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        ContentBackgroundOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        RightButtonTitleBarInteractiveOutsideBehavior: null,
        RightButtonTitleBarDragZoneOutsideBehavior: null,
        RightButtonContentInteractiveOutsideBehavior: OverlayPointerBehavior.CloseAndPassThrough,
        RightButtonContentBackgroundOutsideBehavior: null,
        DefaultOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        OutsidePassthroughTargets:
            OverlayOutsidePassthroughTargets.TitleBarInteractive |
            OverlayOutsidePassthroughTargets.TitleBarDragZone,
        CloseOnOutsideClick: true,
        CloseOnEscape: true,
        ResetAnchorOnClose: true,
        EscapeReservedWhileCapturing: false,
        KeepAncestorChainWhenChildInterceptsClose: true,
        CloseDescendantsOnParentClose: true,
        CloseDescendantsOnAncestorSurfaceHit: true,
        CloseSiblingBranchesOnChainInteraction: true);
    private static readonly OverlayInteractionProfile ToolPanelLikeProfile = new(
        LeftAnchorBehavior: OverlayPointerBehavior.CloseAndPassThrough,
        RightAnchorBehavior: OverlayPointerBehavior.CloseAndPassThrough,
        SurfaceBehaviorWhenClosingOthers: OverlayPointerBehavior.CloseAndPassThrough,
        SurfaceBehaviorWhenStable: OverlayPointerBehavior.KeepOpen,
        ChildOverlayBehaviorWhenClosingOthers: OverlayPointerBehavior.CloseAndPassThrough,
        ChildOverlayBehaviorWhenStable: OverlayPointerBehavior.KeepOpen,
        TitleBarInteractiveOutsideBehavior: OverlayPointerBehavior.CloseAndPassThrough,
        TitleBarDragZoneOutsideBehavior: OverlayPointerBehavior.CloseAndPassThrough,
        ContentInteractiveOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        ContentBackgroundOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        RightButtonTitleBarInteractiveOutsideBehavior: null,
        RightButtonTitleBarDragZoneOutsideBehavior: null,
        RightButtonContentInteractiveOutsideBehavior: null,
        RightButtonContentBackgroundOutsideBehavior: null,
        DefaultOutsideBehavior: OverlayPointerBehavior.CloseAndConsume,
        OutsidePassthroughTargets:
            OverlayOutsidePassthroughTargets.TitleBarInteractive |
            OverlayOutsidePassthroughTargets.TitleBarDragZone,
        CloseOnOutsideClick: true,
        CloseOnEscape: true,
        ResetAnchorOnClose: true,
        EscapeReservedWhileCapturing: false,
        KeepAncestorChainWhenChildInterceptsClose: true,
        CloseDescendantsOnParentClose: true,
        CloseDescendantsOnAncestorSurfaceHit: true,
        CloseSiblingBranchesOnChainInteraction: true);

    internal static void ApplyPreset(AnimatedOverlay overlay, OverlayInteractionPreset preset)
    {
        if (preset == OverlayInteractionPreset.None)
            return;

        Apply(overlay, GetProfile(preset));
    }

    internal static OverlayPointerBehavior ResolveAnchorBehavior(AnimatedOverlay overlay, MouseButton button)
        => ResolveAnchorBehavior(overlay.InteractionPreset, button, overlay.LeftAnchorClickBehavior, overlay.RightAnchorClickBehavior);

    internal static OverlayPointerBehavior ResolveAnchorBehavior(
        OverlayInteractionPreset preset,
        MouseButton button,
        OverlayPointerBehavior leftAnchorBehavior,
        OverlayPointerBehavior rightAnchorBehavior)
    {
        if (TryGetProfile(preset, out var profile))
        {
            return button == MouseButton.Right
                ? profile.RightAnchorBehavior
                : profile.LeftAnchorBehavior;
        }

        return button == MouseButton.Right
            ? rightAnchorBehavior
            : leftAnchorBehavior;
    }

    internal static OverlayPointerBehavior ResolveOutsideBehavior(AnimatedOverlay overlay, MouseButton button, OverlayOutsideHitKind outsideKind)
        => ResolveOutsideBehavior(overlay.InteractionPreset, IsCaptureActive(overlay), button, outsideKind, overlay.OutsidePointerBehavior, overlay.OutsidePassthroughTargets);

    internal static OverlayPointerBehavior ResolveChainBehavior(
        OverlayInteractionPreset preset,
        OverlayHitKind hitKind,
        bool hasClosingOverlays)
    {
        if (!TryGetProfile(preset, out var profile))
        {
            return hasClosingOverlays
                ? OverlayPointerBehavior.CloseAndPassThrough
                : OverlayPointerBehavior.KeepOpen;
        }

        return hitKind switch
        {
            OverlayHitKind.Surface => hasClosingOverlays
                ? profile.SurfaceBehaviorWhenClosingOthers
                : profile.SurfaceBehaviorWhenStable,
            OverlayHitKind.ChildOverlay => hasClosingOverlays
                ? profile.ChildOverlayBehaviorWhenClosingOthers
                : profile.ChildOverlayBehaviorWhenStable,
            _ => hasClosingOverlays
                ? OverlayPointerBehavior.CloseAndPassThrough
                : OverlayPointerBehavior.KeepOpen,
        };
    }

    internal static bool ShouldKeepAncestorChainWhenChildInterceptsClose(OverlayInteractionPreset preset)
        => TryGetProfile(preset, out var profile)
            ? profile.KeepAncestorChainWhenChildInterceptsClose
            : true;

    internal static bool ShouldCloseDescendantsOnParentClose(OverlayInteractionPreset preset)
        => TryGetProfile(preset, out var profile)
            ? profile.CloseDescendantsOnParentClose
            : true;

    internal static bool ShouldCloseDescendantsOnAncestorSurfaceHit(OverlayInteractionPreset preset)
        => TryGetProfile(preset, out var profile)
            ? profile.CloseDescendantsOnAncestorSurfaceHit
            : true;

    internal static bool ShouldCloseSiblingBranchesOnChainInteraction(OverlayInteractionPreset preset)
        => TryGetProfile(preset, out var profile)
            ? profile.CloseSiblingBranchesOnChainInteraction
            : true;

    internal static OverlayPointerBehavior ResolveOutsideBehavior(
        OverlayInteractionPreset preset,
        bool isCaptureActive,
        MouseButton button,
        OverlayOutsideHitKind outsideKind,
        OverlayPointerBehavior outsidePointerBehavior,
        OverlayOutsidePassthroughTargets outsidePassthroughTargets)
    {
        if (preset == OverlayInteractionPreset.CaptureLike && isCaptureActive)
            return OverlayPointerBehavior.CloseAndConsume;

        if (TryGetProfile(preset, out var profile))
            return ResolveProfileOutsideBehavior(profile, button, outsideKind);

        return ResolvePropertyDrivenOutsideBehavior(outsideKind, outsidePointerBehavior, outsidePassthroughTargets);
    }

    internal static bool ShouldCloseOnEscape(AnimatedOverlay overlay)
        => ShouldCloseOnEscape(overlay.InteractionPreset, IsCaptureActive(overlay), overlay.CloseOnEscape);

    internal static bool ShouldCloseOnEscape(
        OverlayInteractionPreset preset,
        bool isCaptureActive,
        bool closeOnEscape)
    {
        var effectiveCloseOnEscape = TryGetProfile(preset, out var profile)
            ? profile.CloseOnEscape
            : closeOnEscape;

        return ResolveCloseRequest(preset, isCaptureActive, OverlayCloseReason.EscapeKey).ShouldClose &&
               effectiveCloseOnEscape;
    }

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
            return GetProfile(preset).EscapeReservedWhileCapturing && isCaptureActive
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

    internal static OverlayInteractionProfile GetProfile(OverlayInteractionPreset preset)
        => preset switch
        {
            OverlayInteractionPreset.MenuLike => MenuLikeProfile,
            OverlayInteractionPreset.CaptureLike => CaptureLikeProfile,
            OverlayInteractionPreset.ContextLike => ContextLikeProfile,
            OverlayInteractionPreset.CardContextLike => CardContextLikeProfile,
            OverlayInteractionPreset.ToolPanelLike => ToolPanelLikeProfile,
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown overlay interaction preset."),
        };

    internal static bool TryGetProfile(OverlayInteractionPreset preset, out OverlayInteractionProfile profile)
    {
        switch (preset)
        {
            case OverlayInteractionPreset.MenuLike:
                profile = MenuLikeProfile;
                return true;
            case OverlayInteractionPreset.CaptureLike:
                profile = CaptureLikeProfile;
                return true;
            case OverlayInteractionPreset.ContextLike:
                profile = ContextLikeProfile;
                return true;
            case OverlayInteractionPreset.CardContextLike:
                profile = CardContextLikeProfile;
                return true;
            case OverlayInteractionPreset.ToolPanelLike:
                profile = ToolPanelLikeProfile;
                return true;
            default:
                profile = default;
                return false;
        }
    }

    private static void Apply(AnimatedOverlay overlay, OverlayInteractionProfile profile)
    {
        overlay.CloseOnOutsideClick = profile.CloseOnOutsideClick;
        overlay.CloseOnEscape = profile.CloseOnEscape;
        overlay.ResetAnchorOnClose = profile.ResetAnchorOnClose;
        overlay.LeftAnchorClickBehavior = profile.LeftAnchorBehavior;
        overlay.RightAnchorClickBehavior = profile.RightAnchorBehavior;
        overlay.OutsidePointerBehavior = profile.DefaultOutsideBehavior;
        overlay.OutsidePassthroughTargets = profile.OutsidePassthroughTargets;
    }

    private static OverlayPointerBehavior ResolveProfileOutsideBehavior(
        OverlayInteractionProfile profile,
        MouseButton button,
        OverlayOutsideHitKind outsideKind)
    {
        if (button == MouseButton.Right)
        {
            var overrideBehavior = outsideKind switch
            {
                OverlayOutsideHitKind.TitleBarInteractive => profile.RightButtonTitleBarInteractiveOutsideBehavior,
                OverlayOutsideHitKind.TitleBarDragZone => profile.RightButtonTitleBarDragZoneOutsideBehavior,
                OverlayOutsideHitKind.ContentInteractive => profile.RightButtonContentInteractiveOutsideBehavior,
                OverlayOutsideHitKind.ContentBackground => profile.RightButtonContentBackgroundOutsideBehavior,
                _ => null,
            };

            if (overrideBehavior.HasValue)
                return overrideBehavior.Value;
        }

        return outsideKind switch
        {
            OverlayOutsideHitKind.TitleBarInteractive => profile.TitleBarInteractiveOutsideBehavior,
            OverlayOutsideHitKind.TitleBarDragZone => profile.TitleBarDragZoneOutsideBehavior,
            OverlayOutsideHitKind.ContentInteractive => profile.ContentInteractiveOutsideBehavior,
            OverlayOutsideHitKind.ContentBackground => profile.ContentBackgroundOutsideBehavior,
            _ => profile.DefaultOutsideBehavior,
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
