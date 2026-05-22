using AniNest.Application.Library;
using AniNest.Application.Playback;
using AniNest.Application.Playlist;
using AniNest.Contracts.Playlist;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal sealed class FileSystemPlaylistCatalogStore : IPlaylistCatalogStore
{
    private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".avi", ".mov", ".wmv"];

    private readonly ILibraryCatalogStore _libraryCatalogStore;
    private readonly IPlaybackProgressStore _progressStore;
    private readonly object _sync = new();
    private readonly Dictionary<string, PlaylistDto> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public FileSystemPlaylistCatalogStore(
        ILibraryCatalogStore libraryCatalogStore,
        IPlaybackProgressStore progressStore)
    {
        _libraryCatalogStore = libraryCatalogStore;
        _progressStore = progressStore;
    }

    public IReadOnlyList<PlaylistDto> GetPlaylists()
        => _libraryCatalogStore.GetFolders().Select(BuildPlaylist).ToArray();

    public PlaylistDto? GetPlaylist(string folderId)
    {
        var folder = _libraryCatalogStore.GetFolders()
            .FirstOrDefault(item => string.Equals(item.FolderId, folderId, StringComparison.OrdinalIgnoreCase));
        if (folder is null)
            return null;

        return BuildPlaylist(folder);
    }

    public void SavePlaylist(PlaylistDto playlist)
    {
        lock (_sync)
        {
            _snapshots[playlist.FolderId] = playlist;
        }
    }

    private PlaylistDto BuildPlaylist(LibraryFolderRecord folder)
    {
        var files = Directory.Exists(folder.Path)
            ? Directory.EnumerateFiles(folder.Path)
                .Where(path => VideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<string>();

        var items = files
            .Select((filePath, index) =>
            {
                var progress = _progressStore.GetVideoProgress(filePath);
                return new PlaylistItemDto(
                    $"ep-{index + 1:00}",
                    index,
                    Path.GetFileNameWithoutExtension(filePath),
                    filePath,
                    progress?.IsPlayed ?? false,
                    progress is not null && progress.Position > 0,
                    progress?.Position ?? 0,
                    progress?.Duration ?? 0,
                    ThumbnailState.Pending);
            })
            .ToArray();

        var generated = new PlaylistDto(
            folder.FolderId,
            folder.Name,
            items.Length > 0 ? items[0].ItemId : null,
            items.Length > 0 ? 0 : -1,
            items);

        lock (_sync)
        {
            if (!_snapshots.TryGetValue(folder.FolderId, out var snapshot))
                return generated;

            return MergeSnapshot(generated, snapshot);
        }
    }

    private static PlaylistDto MergeSnapshot(PlaylistDto generated, PlaylistDto snapshot)
    {
        if (generated.Items.Count == 0)
            return generated;

        var snapshotItems = snapshot.Items.ToDictionary(item => item.ItemId, StringComparer.OrdinalIgnoreCase);
        var mergedItems = generated.Items
            .Select(item =>
            {
                if (!snapshotItems.TryGetValue(item.ItemId, out var saved))
                    return item;

                return item with
                {
                    IsPlayed = saved.IsPlayed,
                    HasSavedProgress = saved.HasSavedProgress,
                    SavedProgressMs = saved.SavedProgressMs,
                    DurationMs = saved.DurationMs,
                    ThumbnailState = saved.ThumbnailState
                };
            })
            .ToArray();

        var currentItemId = mergedItems.Any(item => string.Equals(item.ItemId, snapshot.CurrentItemId, StringComparison.OrdinalIgnoreCase))
            ? snapshot.CurrentItemId
            : generated.CurrentItemId;

        var currentIndex = currentItemId is null
            ? -1
            : Array.FindIndex(mergedItems, item => string.Equals(item.ItemId, currentItemId, StringComparison.OrdinalIgnoreCase));

        return generated with
        {
            CurrentItemId = currentItemId,
            CurrentIndex = currentIndex,
            Items = mergedItems
        };
    }
}
