namespace AniNest.Presentation.Overlays;

internal readonly record struct OverlayCloseRequestDecision(
    bool IsHandled,
    bool ShouldClose,
    string Detail)
{
    public static OverlayCloseRequestDecision Close(string detail = "close")
        => new(false, true, detail);

    public static OverlayCloseRequestDecision Ignore(string detail)
        => new(false, false, detail);

    public static OverlayCloseRequestDecision Intercept(string detail)
        => new(true, false, detail);
}
