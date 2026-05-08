using System.Windows.Input;
using AniNest.Presentation.Overlays;

namespace AniNest.Tests.View.Overlays;

public class OverlayInteractionPresetsTests
{
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
            OverlayOutsideHitKind.ContentInteractive,
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
            OverlayOutsideHitKind.TitleBarDragZone,
            OverlayPointerBehavior.CloseAndConsume,
            OverlayOutsidePassthroughTargets.TitleBarDragZone);

        result.Should().Be(OverlayPointerBehavior.CloseAndPassThrough);
    }
}
