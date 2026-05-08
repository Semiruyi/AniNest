namespace AniNest.Presentation.Overlays;

internal readonly record struct OverlayInteractionProfile(
    OverlayPointerBehavior LeftAnchorBehavior,
    OverlayPointerBehavior RightAnchorBehavior,
    OverlayPointerBehavior SurfaceBehaviorWhenClosingOthers,
    OverlayPointerBehavior SurfaceBehaviorWhenStable,
    OverlayPointerBehavior ChildOverlayBehaviorWhenClosingOthers,
    OverlayPointerBehavior ChildOverlayBehaviorWhenStable,
    OverlayPointerBehavior TitleBarInteractiveOutsideBehavior,
    OverlayPointerBehavior TitleBarDragZoneOutsideBehavior,
    OverlayPointerBehavior ContentInteractiveOutsideBehavior,
    OverlayPointerBehavior ContentBackgroundOutsideBehavior,
    OverlayPointerBehavior DefaultOutsideBehavior,
    OverlayOutsidePassthroughTargets OutsidePassthroughTargets,
    bool CloseOnOutsideClick,
    bool CloseOnEscape,
    bool ResetAnchorOnClose,
    bool EscapeReservedWhileCapturing,
    bool KeepAncestorChainWhenChildInterceptsClose,
    bool CloseDescendantsOnParentClose,
    bool CloseDescendantsOnAncestorSurfaceHit,
    bool CloseSiblingBranchesOnChainInteraction);
