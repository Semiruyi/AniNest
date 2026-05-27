using AniNest.Application.Playback;

namespace AniNest.Backend.Tests;

internal sealed class InMemoryPlaybackProgressStore : IPlaybackProgressStore
{
    private readonly Dictionary<string, VideoProgressState> _videoProgress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FolderProgressState> _folderProgress = new(StringComparer.OrdinalIgnoreCase);
    private PlaybackSessionState? _lastSession;

    public VideoProgressState? GetVideoProgress(string filePath)
        => _videoProgress.TryGetValue(filePath, out var progress) ? progress : null;

    public void SaveVideoProgress(string filePath, long position, long duration)
    {
        _videoProgress[filePath] = new VideoProgressState(filePath, position, duration, true);
    }

    public void MarkVideoPlayed(string filePath)
    {
        var existing = GetVideoProgress(filePath);
        _videoProgress[filePath] = existing is null
            ? new VideoProgressState(filePath, 0, 0, true)
            : existing with { IsPlayed = true };
    }

    public FolderProgressState? GetFolderProgress(string folderId)
        => _folderProgress.TryGetValue(folderId, out var progress) ? progress : null;

    public void SaveFolderProgress(string folderId, string lastItemId)
    {
        _folderProgress[folderId] = new FolderProgressState(folderId, lastItemId);
    }

    public PlaybackSessionState? GetLastSession()
        => _lastSession;

    public void SaveLastSession(PlaybackSessionState session)
    {
        _lastSession = session;
    }

    public void ClearLastSession()
    {
        _lastSession = null;
    }
}
