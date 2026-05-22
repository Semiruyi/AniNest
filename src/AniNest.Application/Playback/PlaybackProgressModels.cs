namespace AniNest.Application.Playback;

public sealed record VideoProgressState(
    string FilePath,
    long Position,
    long Duration,
    bool IsPlayed);

public sealed record FolderProgressState(
    string FolderId,
    string LastItemId);
