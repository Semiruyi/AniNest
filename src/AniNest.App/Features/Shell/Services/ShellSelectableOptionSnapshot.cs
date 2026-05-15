namespace AniNest.Features.Shell.Services;

public sealed record ShellSelectableOptionSnapshot(
    string Code,
    string DisplayName,
    bool IsSelected);
