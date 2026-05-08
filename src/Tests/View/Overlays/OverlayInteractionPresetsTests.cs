using System.Windows.Input;
using AniNest.Presentation.Overlays;

namespace AniNest.Tests.View.Overlays;

public class OverlayInteractionPresetsTests
{
    [Fact]
    public void GetProfile_MenuLike_ExposesExpectedDefaults()
    {
        var profile = OverlayInteractionPresets.GetProfile(OverlayInteractionPreset.MenuLike);

        profile.LeftAnchorBehavior.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.RightAnchorBehavior.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.SurfaceBehaviorWhenClosingOthers.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.SurfaceBehaviorWhenStable.Should().Be(OverlayPointerBehavior.KeepOpen);
        profile.ChildOverlayBehaviorWhenClosingOthers.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.ChildOverlayBehaviorWhenStable.Should().Be(OverlayPointerBehavior.KeepOpen);
        profile.TitleBarInteractiveOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.TitleBarDragZoneOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.ContentInteractiveOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndConsume);
        profile.ContentBackgroundOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndConsume);
        profile.DefaultOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndConsume);
        profile.OutsidePassthroughTargets.Should().Be(
            OverlayOutsidePassthroughTargets.TitleBarInteractive |
            OverlayOutsidePassthroughTargets.TitleBarDragZone);
        profile.CloseOnOutsideClick.Should().BeTrue();
        profile.CloseOnEscape.Should().BeTrue();
        profile.ResetAnchorOnClose.Should().BeTrue();
        profile.EscapeReservedWhileCapturing.Should().BeFalse();
        profile.KeepAncestorChainWhenChildInterceptsClose.Should().BeTrue();
        profile.CloseDescendantsOnParentClose.Should().BeTrue();
        profile.CloseDescendantsOnAncestorSurfaceHit.Should().BeTrue();
        profile.CloseSiblingBranchesOnChainInteraction.Should().BeTrue();
    }

    [Fact]
    public void GetProfile_CaptureLike_ReservesEscapeWhileCapturing()
    {
        var profile = OverlayInteractionPresets.GetProfile(OverlayInteractionPreset.CaptureLike);

        profile.LeftAnchorBehavior.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.RightAnchorBehavior.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.SurfaceBehaviorWhenClosingOthers.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.SurfaceBehaviorWhenStable.Should().Be(OverlayPointerBehavior.KeepOpen);
        profile.ChildOverlayBehaviorWhenClosingOthers.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.ChildOverlayBehaviorWhenStable.Should().Be(OverlayPointerBehavior.KeepOpen);
        profile.TitleBarInteractiveOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.TitleBarDragZoneOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.ContentInteractiveOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndConsume);
        profile.ContentBackgroundOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndConsume);
        profile.DefaultOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndConsume);
        profile.OutsidePassthroughTargets.Should().Be(
            OverlayOutsidePassthroughTargets.TitleBarInteractive |
            OverlayOutsidePassthroughTargets.TitleBarDragZone);
        profile.EscapeReservedWhileCapturing.Should().BeTrue();
        profile.KeepAncestorChainWhenChildInterceptsClose.Should().BeTrue();
        profile.CloseDescendantsOnParentClose.Should().BeTrue();
        profile.CloseDescendantsOnAncestorSurfaceHit.Should().BeTrue();
        profile.CloseSiblingBranchesOnChainInteraction.Should().BeTrue();
    }

    [Fact]
    public void GetProfile_CardContextLike_AllowsRightClickPassThroughOnContentInteractive()
    {
        var profile = OverlayInteractionPresets.GetProfile(OverlayInteractionPreset.CardContextLike);

        profile.TitleBarInteractiveOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.TitleBarDragZoneOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.ContentInteractiveOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndConsume);
        profile.RightButtonContentInteractiveOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
    }

    [Fact]
    public void GetProfile_ToolPanelLike_PassesThroughOutsideInteraction()
    {
        var profile = OverlayInteractionPresets.GetProfile(OverlayInteractionPreset.ToolPanelLike);

        profile.LeftAnchorBehavior.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.RightAnchorBehavior.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.TitleBarInteractiveOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.TitleBarDragZoneOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
        profile.ContentInteractiveOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndConsume);
        profile.ContentBackgroundOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndConsume);
        profile.DefaultOutsideBehavior.Should().Be(OverlayPointerBehavior.CloseAndConsume);
        profile.OutsidePassthroughTargets.Should().Be(
            OverlayOutsidePassthroughTargets.TitleBarInteractive |
            OverlayOutsidePassthroughTargets.TitleBarDragZone);
    }

    [Fact]
    public void ResolveAnchorBehavior_ContextLike_ConsumesAnchorClick()
    {
        var result = OverlayInteractionPresets.ResolveAnchorBehavior(
            OverlayInteractionPreset.ContextLike,
            MouseButton.Right,
            OverlayPointerBehavior.KeepOpen,
            OverlayPointerBehavior.CloseAndPassThrough);

        result.Should().Be(OverlayPointerBehavior.CloseAndConsume);
    }

    [Fact]
    public void ResolveOutsideBehavior_MenuLike_PassesThroughTitleBarInteractive()
    {
        var result = OverlayInteractionPresets.ResolveOutsideBehavior(
            OverlayInteractionPreset.MenuLike,
            isCaptureActive: false,
            MouseButton.Left,
            OverlayOutsideHitKind.TitleBarInteractive,
            OverlayPointerBehavior.CloseAndConsume,
            OverlayOutsidePassthroughTargets.None);

        result.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
    }

    [Fact]
    public void ResolveOutsideBehavior_ContextLike_ConsumesContentInteractive()
    {
        var result = OverlayInteractionPresets.ResolveOutsideBehavior(
            OverlayInteractionPreset.ContextLike,
            isCaptureActive: false,
            MouseButton.Left,
            OverlayOutsideHitKind.ContentInteractive,
            OverlayPointerBehavior.CloseAndPassThrough,
            OverlayOutsidePassthroughTargets.TitleBarInteractive);

        result.Should().Be(OverlayPointerBehavior.CloseAndConsume);
    }

    [Fact]
    public void ResolveChainBehavior_MenuLike_Surface_WhenStable_KeepsOpen()
    {
        var result = OverlayInteractionPresets.ResolveChainBehavior(
            OverlayInteractionPreset.MenuLike,
            OverlayHitKind.Surface,
            hasClosingOverlays: false);

        result.Should().Be(OverlayPointerBehavior.KeepOpen);
    }

    [Fact]
    public void ResolveChainBehavior_MenuLike_ChildOverlay_WhenClosingOthers_PassesThrough()
    {
        var result = OverlayInteractionPresets.ResolveChainBehavior(
            OverlayInteractionPreset.MenuLike,
            OverlayHitKind.ChildOverlay,
            hasClosingOverlays: true);

        result.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
    }

    [Fact]
    public void ResolveOutsideBehavior_CaptureLike_WhenCapturing_AlwaysConsumes()
    {
        var result = OverlayInteractionPresets.ResolveOutsideBehavior(
            OverlayInteractionPreset.CaptureLike,
            isCaptureActive: true,
            MouseButton.Left,
            OverlayOutsideHitKind.TitleBarInteractive,
            OverlayPointerBehavior.CloseAndPassThrough,
            OverlayOutsidePassthroughTargets.TitleBarInteractive);

        result.Should().Be(OverlayPointerBehavior.CloseAndConsume);
    }

    [Fact]
    public void ResolveCloseRequest_CaptureLike_WhenCapturingAndOutsideClick_InterceptsClose()
    {
        var result = OverlayInteractionPresets.ResolveCloseRequest(
            OverlayInteractionPreset.CaptureLike,
            isCaptureActive: true,
            OverlayCloseReason.OutsideClick);

        result.IsHandled.Should().BeTrue();
        result.ShouldClose.Should().BeFalse();
        result.Detail.Should().Be("outside-click-canceled-active-capture");
    }

    [Fact]
    public void ResolveCloseRequest_CaptureLike_WhenCapturingAndEscape_IgnoresClose()
    {
        var result = OverlayInteractionPresets.ResolveCloseRequest(
            OverlayInteractionPreset.CaptureLike,
            isCaptureActive: true,
            OverlayCloseReason.EscapeKey);

        result.IsHandled.Should().BeFalse();
        result.ShouldClose.Should().BeFalse();
        result.Detail.Should().Be("escape-reserved-for-capture");
    }

    [Fact]
    public void ShouldCloseOnEscape_CaptureLike_WhenCapturing_ReturnsFalse()
    {
        var result = OverlayInteractionPresets.ShouldCloseOnEscape(
            OverlayInteractionPreset.CaptureLike,
            isCaptureActive: true,
            closeOnEscape: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldCloseOnEscape_CaptureLike_WhenNotCapturing_ReturnsTrue()
    {
        var result = OverlayInteractionPresets.ShouldCloseOnEscape(
            OverlayInteractionPreset.CaptureLike,
            isCaptureActive: false,
            closeOnEscape: true);

        result.Should().BeTrue();
    }

    [Fact]
    public void ResolveOutsideBehavior_None_UsesPassthroughTargets()
    {
        var result = OverlayInteractionPresets.ResolveOutsideBehavior(
            OverlayInteractionPreset.None,
            isCaptureActive: false,
            MouseButton.Left,
            OverlayOutsideHitKind.TitleBarDragZone,
            OverlayPointerBehavior.CloseAndConsume,
            OverlayOutsidePassthroughTargets.TitleBarDragZone);

        result.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
    }

    [Fact]
    public void ResolveOutsideBehavior_CardContextLike_RightClickContentInteractive_PassesThrough()
    {
        var result = OverlayInteractionPresets.ResolveOutsideBehavior(
            OverlayInteractionPreset.CardContextLike,
            isCaptureActive: false,
            MouseButton.Right,
            OverlayOutsideHitKind.ContentInteractive,
            OverlayPointerBehavior.CloseAndConsume,
            OverlayOutsidePassthroughTargets.None);

        result.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
    }

    [Fact]
    public void ResolveOutsideBehavior_CardContextLike_LeftClickContentInteractive_Consumes()
    {
        var result = OverlayInteractionPresets.ResolveOutsideBehavior(
            OverlayInteractionPreset.CardContextLike,
            isCaptureActive: false,
            MouseButton.Left,
            OverlayOutsideHitKind.ContentInteractive,
            OverlayPointerBehavior.CloseAndConsume,
            OverlayOutsidePassthroughTargets.None);

        result.Should().Be(OverlayPointerBehavior.CloseAndConsume);
    }

    [Fact]
    public void ResolveOutsideBehavior_ToolPanelLike_LeftClickContentInteractive_PassesThrough()
    {
        var result = OverlayInteractionPresets.ResolveOutsideBehavior(
            OverlayInteractionPreset.ToolPanelLike,
            isCaptureActive: false,
            MouseButton.Left,
            OverlayOutsideHitKind.ContentInteractive,
            OverlayPointerBehavior.CloseAndConsume,
            OverlayOutsidePassthroughTargets.None);

        result.Should().Be(OverlayPointerBehavior.CloseAndConsume);
    }

    [Fact]
    public void ResolveOutsideBehavior_ToolPanelLike_LeftClickTitleBarInteractive_PassesThrough()
    {
        var result = OverlayInteractionPresets.ResolveOutsideBehavior(
            OverlayInteractionPreset.ToolPanelLike,
            isCaptureActive: false,
            MouseButton.Left,
            OverlayOutsideHitKind.TitleBarInteractive,
            OverlayPointerBehavior.CloseAndConsume,
            OverlayOutsidePassthroughTargets.None);

        result.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
    }

    [Fact]
    public void TryGetProfile_None_ReturnsFalse()
    {
        var found = OverlayInteractionPresets.TryGetProfile(OverlayInteractionPreset.None, out var profile);

        found.Should().BeFalse();
        profile.LeftAnchorBehavior.Should().Be(OverlayPointerBehavior.KeepOpen);
        profile.RightAnchorBehavior.Should().Be(OverlayPointerBehavior.KeepOpen);
        profile.SurfaceBehaviorWhenClosingOthers.Should().Be(OverlayPointerBehavior.KeepOpen);
        profile.SurfaceBehaviorWhenStable.Should().Be(OverlayPointerBehavior.KeepOpen);
        profile.ChildOverlayBehaviorWhenClosingOthers.Should().Be(OverlayPointerBehavior.KeepOpen);
        profile.ChildOverlayBehaviorWhenStable.Should().Be(OverlayPointerBehavior.KeepOpen);
        profile.TitleBarInteractiveOutsideBehavior.Should().Be(OverlayPointerBehavior.KeepOpen);
        profile.TitleBarDragZoneOutsideBehavior.Should().Be(OverlayPointerBehavior.KeepOpen);
        profile.ContentInteractiveOutsideBehavior.Should().Be(OverlayPointerBehavior.KeepOpen);
        profile.ContentBackgroundOutsideBehavior.Should().Be(OverlayPointerBehavior.KeepOpen);
        profile.RightButtonTitleBarInteractiveOutsideBehavior.Should().BeNull();
        profile.RightButtonTitleBarDragZoneOutsideBehavior.Should().BeNull();
        profile.RightButtonContentInteractiveOutsideBehavior.Should().BeNull();
        profile.RightButtonContentBackgroundOutsideBehavior.Should().BeNull();
        profile.DefaultOutsideBehavior.Should().Be(OverlayPointerBehavior.KeepOpen);
        profile.OutsidePassthroughTargets.Should().Be(OverlayOutsidePassthroughTargets.None);
        profile.CloseOnOutsideClick.Should().BeFalse();
        profile.CloseOnEscape.Should().BeFalse();
        profile.ResetAnchorOnClose.Should().BeFalse();
        profile.EscapeReservedWhileCapturing.Should().BeFalse();
        profile.KeepAncestorChainWhenChildInterceptsClose.Should().BeFalse();
        profile.CloseDescendantsOnParentClose.Should().BeFalse();
        profile.CloseDescendantsOnAncestorSurfaceHit.Should().BeFalse();
        profile.CloseSiblingBranchesOnChainInteraction.Should().BeFalse();
    }

    [Fact]
    public void ShouldKeepAncestorChainWhenChildInterceptsClose_MenuLike_ReturnsTrue()
    {
        var result = OverlayInteractionPresets.ShouldKeepAncestorChainWhenChildInterceptsClose(
            OverlayInteractionPreset.MenuLike);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldCloseDescendantsOnParentClose_ContextLike_ReturnsTrue()
    {
        var result = OverlayInteractionPresets.ShouldCloseDescendantsOnParentClose(
            OverlayInteractionPreset.ContextLike);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldCloseDescendantsOnAncestorSurfaceHit_MenuLike_ReturnsTrue()
    {
        var result = OverlayInteractionPresets.ShouldCloseDescendantsOnAncestorSurfaceHit(
            OverlayInteractionPreset.MenuLike);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldCloseSiblingBranchesOnChainInteraction_CaptureLike_ReturnsTrue()
    {
        var result = OverlayInteractionPresets.ShouldCloseSiblingBranchesOnChainInteraction(
            OverlayInteractionPreset.CaptureLike);

        result.Should().BeTrue();
    }
}
