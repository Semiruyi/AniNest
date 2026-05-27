namespace AniNest.Application.Playback;

public sealed record VideoProgressState(
    string FilePath,
    long Position,
    long Duration,
    bool IsPlayed);

public sealed record FolderProgressState(
    string FolderId,
    string LastItemId);

public sealed record PlaybackSessionState(
    string FolderId,
    string CurrentItemId,
    double PreferredRate,
    int PreferredVolume);
