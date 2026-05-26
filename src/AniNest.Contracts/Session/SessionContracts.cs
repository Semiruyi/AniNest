namespace AniNest.Contracts.Session;

public sealed record PlaybackTargetDto(
    string ItemId,
    string Title,
    string MediaUrl,
    long StartPositionMs,
    string? SubtitleUrl = null,
    string? AudioTrackHint = null);

public sealed record SessionStateDto(
    string SessionId,
    string FolderId,
    string FolderName,
    string CurrentItemId,
    int CurrentIndex,
    int PlaylistCount,
    bool HasPrevious,
    bool HasNext,
    long SavedProgressMs,
    double PreferredRate,
    int PreferredVolume);

public sealed record SessionOpenFolderRequest(
    string FolderId);

public sealed record SessionSelectItemRequest(
    string ItemId);

public sealed record SessionProgressReportRequest(
    string ItemId,
    long PositionMs,
    long DurationMs,
    double Rate,
    int Volume,
    bool IsPaused);

public sealed record SessionCompleteRequest(
    string ItemId);

public sealed record SessionOpenResultDto(
    SessionStateDto Session,
    PlaybackTargetDto PlaybackTarget);
