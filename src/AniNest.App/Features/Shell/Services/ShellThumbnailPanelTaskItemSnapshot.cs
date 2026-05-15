namespace AniNest.Features.Shell.Services;

public sealed record ShellThumbnailPanelTaskItemSnapshot(
    string FileName,
    string IntentText,
    string StatusText,
    string StatusCode,
    int ProgressPercent,
    bool IsSuspended);
