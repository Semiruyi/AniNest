namespace AniNest.Features.Shell.Services;

public sealed record ShellOptionsPanelSnapshot(
    IReadOnlyList<ShellSelectableOptionSnapshot> LanguageOptions,
    IReadOnlyList<ShellSelectableOptionSnapshot> FullscreenAnimationOptions);
