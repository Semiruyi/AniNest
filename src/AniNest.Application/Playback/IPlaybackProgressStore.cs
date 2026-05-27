namespace AniNest.Application.Playback;

public interface IPlaybackProgressStore
{
    VideoProgressState? GetVideoProgress(string filePath);
    void SaveVideoProgress(string filePath, long position, long duration);
    void MarkVideoPlayed(string filePath);
    FolderProgressState? GetFolderProgress(string folderId);
    void SaveFolderProgress(string folderId, string lastItemId);
    PlaybackSessionState? GetLastSession();
    void SaveLastSession(PlaybackSessionState session);
    void ClearLastSession();
}
