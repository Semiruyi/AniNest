using AniNest.Contracts.Playlist;
using AniNest.Contracts.Session;
using AniNest.Contracts.Settings;
using AniNest.Application.Playlist;
using AniNest.Application.Resources;

namespace AniNest.Application.Playback;

public sealed class PlaybackSessionEngine
{
    private const double ResumeCompletionThreshold = 0.9;
    private readonly PlaylistCatalogService _playlistCatalog;
    private readonly IPlaybackProgressStore _progressStore;
    private readonly IResourceUrlService _resourceUrlService;
    private SessionStateDto? _currentSession;
    private PlayerSettingsDto _playerSettings;

    public PlaybackSessionEngine(
        PlaylistCatalogService playlistCatalog,
        PlayerSettingsDto playerSettings,
        IPlaybackProgressStore progressStore,
        IResourceUrlService resourceUrlService)
    {
        _playlistCatalog = playlistCatalog;
        _playerSettings = playerSettings;
        _progressStore = progressStore;
        _resourceUrlService = resourceUrlService;
    }

    public SessionStateDto? CurrentSession => _currentSession;

    public PlaylistDto? GetCurrentPlaylist()
    {
        if (_currentSession is null)
            return null;

        return GetPlaylist(_currentSession.FolderId);
    }

    public PlaylistDto GetPlaylist(string folderId)
        => _playlistCatalog.GetPlaylist(folderId);

    public FolderActivationResult ActivateFolder(string folderId)
    {
        var playlist = GetPlaylist(folderId);
        var item = ResolveStartItem(playlist);
        var startPositionMs = ResolveStartPosition(item);

        if (_currentSession is not null &&
            string.Equals(_currentSession.FolderId, folderId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(_currentSession.CurrentItemId, item.ItemId, StringComparison.OrdinalIgnoreCase) &&
            _currentSession.SavedProgressMs == startPositionMs)
        {
            return new FolderActivationResult(
                BuildOpenResult(item, startPositionMs, _currentSession),
                SessionChanged: false);
        }

        return new FolderActivationResult(
            SetCurrent(folderId, item.ItemId, startPositionMs),
            SessionChanged: true);
    }

    public SessionOpenResultDto SelectItem(string itemId)
    {
        var (playlist, item) = ResolveSessionItem(itemId);
        return SetCurrent(playlist.FolderId, item.ItemId, item.SavedProgressMs);
    }

    public SessionOpenResultDto MoveNext()
    {
        var (playlist, item) = GetAdjacent(offset: 1);
        return SetCurrent(playlist.FolderId, item.ItemId, item.SavedProgressMs);
    }

    public SessionOpenResultDto MovePrevious()
    {
        var (playlist, item) = GetAdjacent(offset: -1);
        return SetCurrent(playlist.FolderId, item.ItemId, item.SavedProgressMs);
    }

    public void ReportProgress(SessionProgressReportRequest request)
    {
        var (playlist, item) = ResolveSessionItem(request.ItemId);
        var updated = item with
        {
            HasSavedProgress = request.PositionMs > 0,
            SavedProgressMs = request.PositionMs,
            DurationMs = request.DurationMs > 0 ? request.DurationMs : item.DurationMs
        };

        ReplacePlaylistItem(playlist, updated);
        _progressStore.SaveVideoProgress(item.FilePath, request.PositionMs, request.DurationMs);
        _progressStore.SaveFolderProgress(playlist.FolderId, item.ItemId);
        _playerSettings = _playerSettings with
        {
            PreferredRate = request.Rate,
            PreferredVolume = request.Volume
        };

        if (_currentSession is not null && string.Equals(_currentSession.CurrentItemId, request.ItemId, StringComparison.OrdinalIgnoreCase))
        {
            _currentSession = _currentSession with
            {
                SavedProgressMs = request.PositionMs,
                PreferredRate = _playerSettings.PreferredRate,
                PreferredVolume = _playerSettings.PreferredVolume
            };
        }
    }

    public void Complete(SessionCompleteRequest request)
    {
        var (playlist, item) = ResolveSessionItem(request.ItemId);
        var updated = item with
        {
            IsPlayed = true,
            HasSavedProgress = false,
            SavedProgressMs = 0
        };

        ReplacePlaylistItem(playlist, updated);
        _progressStore.MarkVideoPlayed(item.FilePath);
        _progressStore.SaveFolderProgress(playlist.FolderId, item.ItemId);

        if (_currentSession is not null && string.Equals(_currentSession.CurrentItemId, request.ItemId, StringComparison.OrdinalIgnoreCase))
            _currentSession = _currentSession with { SavedProgressMs = 0 };
    }

    public void Close()
    {
        if (_currentSession is not null)
            _progressStore.SaveFolderProgress(_currentSession.FolderId, _currentSession.CurrentItemId);

        _currentSession = null;
    }

    private PlaylistItemDto ResolveStartItem(PlaylistDto playlist)
    {
        var folderProgress = _progressStore.GetFolderProgress(playlist.FolderId);
        if (folderProgress is not null)
        {
            var resumed = playlist.Items.FirstOrDefault(item => string.Equals(item.ItemId, folderProgress.LastItemId, StringComparison.OrdinalIgnoreCase));
            if (resumed is not null)
                return resumed;
        }

        if (!string.IsNullOrWhiteSpace(playlist.CurrentItemId))
        {
            var existing = playlist.Items.FirstOrDefault(item => string.Equals(item.ItemId, playlist.CurrentItemId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                return existing;
        }

        return playlist.Items.First();
    }

    private long ResolveStartPosition(PlaylistItemDto item)
    {
        var progress = _progressStore.GetVideoProgress(item.FilePath);
        if (progress is null)
            return item.SavedProgressMs;

        if (progress.Duration > 0 && progress.Position > progress.Duration * ResumeCompletionThreshold)
            return 0;

        return progress.Position;
    }

    private (PlaylistDto Playlist, PlaylistItemDto Item) ResolveItem(string itemId)
        => _playlistCatalog.ResolveItem(itemId);

    private (PlaylistDto Playlist, PlaylistItemDto Item) ResolveSessionItem(string itemId)
    {
        if (_currentSession is not null)
        {
            var currentPlaylist = GetPlaylist(_currentSession.FolderId);
            var currentItem = currentPlaylist.Items.FirstOrDefault(
                item => string.Equals(item.ItemId, itemId, StringComparison.OrdinalIgnoreCase));
            if (currentItem is not null)
                return (currentPlaylist, currentItem);
        }

        return ResolveItem(itemId);
    }

    private (PlaylistDto Playlist, PlaylistItemDto Item) GetAdjacent(int offset)
    {
        if (_currentSession is null)
            throw new InvalidOperationException("No active session.");

        var playlist = GetPlaylist(_currentSession.FolderId);
        var currentIndex = Math.Clamp(_currentSession.CurrentIndex + offset, 0, playlist.Items.Count - 1);
        return (playlist, playlist.Items[currentIndex]);
    }

    private SessionOpenResultDto SetCurrent(string folderId, string itemId, long savedProgressMs)
    {
        var playlist = GetPlaylist(folderId);
        var currentIndex = IndexOfItem(playlist, itemId);
        if (currentIndex < 0)
            throw new KeyNotFoundException($"Playlist item '{itemId}' was not found in folder '{folderId}'.");

        var item = playlist.Items[currentIndex];
        var updatedPlaylist = playlist with
        {
            CurrentItemId = item.ItemId,
            CurrentIndex = currentIndex
        };
        _playlistCatalog.Save(updatedPlaylist);

        _currentSession = BuildSession(updatedPlaylist, item.ItemId, savedProgressMs);
        return BuildOpenResult(item, savedProgressMs, _currentSession);
    }

    private SessionOpenResultDto BuildOpenResult(
        PlaylistItemDto item,
        long savedProgressMs,
        SessionStateDto session)
    {
        var playbackMediaOwnerId = BuildPlaybackMediaOwnerId(
            session.FolderId,
            item.ItemId);

        return new SessionOpenResultDto(
            session,
            new PlaybackTargetDto(
                item.ItemId,
                item.Title,
                _resourceUrlService.GetUrl(
                    new ResourceKey(
                        ResourceKind.PlaybackMedia,
                        playbackMediaOwnerId)),
                savedProgressMs));
    }

    private void ReplacePlaylistItem(PlaylistDto playlist, PlaylistItemDto item)
    {
        var items = playlist.Items.ToArray();
        items[item.Index] = item;
        var currentIndex = string.Equals(playlist.CurrentItemId, item.ItemId, StringComparison.OrdinalIgnoreCase)
            ? item.Index
            : playlist.CurrentIndex;
        _playlistCatalog.Save(playlist with
        {
            Items = items,
            CurrentIndex = currentIndex
        });
    }

    private SessionStateDto BuildSession(PlaylistDto playlist, string itemId, long savedProgressMs)
    {
        var currentIndex = IndexOfItem(playlist, itemId);

        return new SessionStateDto(
            $"session-{playlist.FolderId}",
            playlist.FolderId,
            playlist.FolderName,
            itemId,
            currentIndex,
            playlist.Items.Count,
            currentIndex > 0,
            currentIndex < playlist.Items.Count - 1,
            savedProgressMs,
            _playerSettings.PreferredRate,
            _playerSettings.PreferredVolume);
    }

    private static int IndexOfItem(PlaylistDto playlist, string itemId)
        => Array.FindIndex(playlist.Items.ToArray(), item => string.Equals(item.ItemId, itemId, StringComparison.OrdinalIgnoreCase));

    private static string BuildPlaybackMediaOwnerId(string folderId, string itemId)
        => $"{folderId}:{itemId}";
}
