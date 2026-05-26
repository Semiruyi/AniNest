using AniNest.Application.Library;
using AniNest.Application.Metadata;
using AniNest.Application.Playlist;
using AniNest.Application.Resources;

namespace AniNest.Host.Modules.Resources;

internal sealed class ResourceLocator : IResourceLocator
{
    private readonly ILibraryCatalogStore _libraryCatalogStore;
    private readonly ILibraryFileScanner _libraryFileScanner;
    private readonly IMetadataStore _metadataStore;
    private readonly IPlaylistCatalogStore _playlistCatalogStore;
    private readonly string _metadataPosterRootPath;

    public ResourceLocator(
        ILibraryCatalogStore libraryCatalogStore,
        ILibraryFileScanner libraryFileScanner,
        IMetadataStore metadataStore,
        IPlaylistCatalogStore playlistCatalogStore,
        string metadataPosterRootPath)
    {
        _libraryCatalogStore = libraryCatalogStore;
        _libraryFileScanner = libraryFileScanner;
        _metadataStore = metadataStore;
        _playlistCatalogStore = playlistCatalogStore;
        _metadataPosterRootPath = metadataPosterRootPath;
    }

    public async Task<ResolvedResource?> ResolveAsync(
        ResourceKey key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = key.Kind switch
        {
            ResourceKind.LibraryCover => await ResolveLibraryCoverPathAsync(
                key.OwnerId,
                cancellationToken),
            ResourceKind.LibraryPoster => ResolveLibraryPosterPath(key.OwnerId),
            ResourceKind.PlaybackMedia => ResolvePlaybackMediaPath(key.OwnerId),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return new ResolvedResource(
            path,
            ResourceContentTypes.FromPath(path));
    }

    private async Task<string?> ResolveLibraryCoverPathAsync(
        string folderId,
        CancellationToken cancellationToken)
    {
        var folders = _libraryCatalogStore.GetFolders().ToList();
        var index = folders.FindIndex(folder =>
            string.Equals(folder.FolderId, folderId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return null;
        }

        var folder = folders[index];
        if (Directory.Exists(folder.Path))
        {
            var scanResult = await _libraryFileScanner.ScanFolderAsync(
                folder.Path,
                cancellationToken);
            var updatedFolder = folder with { CoverPath = scanResult.CoverPath };
            if (updatedFolder != folder)
            {
                folders[index] = updatedFolder;
                _libraryCatalogStore.SaveFolders(folders);
                folder = updatedFolder;
            }
        }

        if (!string.IsNullOrWhiteSpace(folder.CoverPath))
            return folder.CoverPath;

        var metadata = _metadataStore.GetByFolderId(folderId);
        if (!string.IsNullOrWhiteSpace(metadata?.PosterPath))
            return ResolveMetadataPosterPath(metadata!.PosterPath);

        return ResolveMetadataPosterPath(folder.MetadataSummary?.PosterPath);
    }

    private string? ResolveLibraryPosterPath(string folderId)
    {
        var metadata = _metadataStore.GetByFolderId(folderId);
        if (!string.IsNullOrWhiteSpace(metadata?.PosterPath))
        {
            return ResolveMetadataPosterPath(metadata!.PosterPath);
        }

        return ResolveMetadataPosterPath(_libraryCatalogStore.GetFolders()
            .FirstOrDefault(folder =>
                string.Equals(folder.FolderId, folderId, StringComparison.OrdinalIgnoreCase))
            ?.MetadataSummary?.PosterPath);
    }

    private string? ResolvePlaybackMediaPath(string ownerId)
    {
        if (TryParsePlaybackMediaOwnerId(ownerId, out var folderId, out var itemId))
        {
            var playlist = _playlistCatalogStore.GetPlaylists()
                .FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.FolderId,
                        folderId,
                        StringComparison.OrdinalIgnoreCase));
            var playlistItem = playlist?.Items.FirstOrDefault(candidate =>
                string.Equals(candidate.ItemId, itemId, StringComparison.OrdinalIgnoreCase));
            if (playlistItem is not null)
            {
                return playlistItem.FilePath;
            }
        }

        foreach (var playlist in _playlistCatalogStore.GetPlaylists())
        {
            var item = playlist.Items.FirstOrDefault(candidate =>
                string.Equals(candidate.ItemId, ownerId, StringComparison.OrdinalIgnoreCase));
            if (item is not null)
            {
                return item.FilePath;
            }
        }

        return null;
    }

    private string? ResolveMetadataPosterPath(string? posterPath)
    {
        if (string.IsNullOrWhiteSpace(posterPath))
            return null;

        if (Path.IsPathRooted(posterPath))
            return posterPath;

        return Path.Combine(_metadataPosterRootPath, posterPath);
    }

    private static bool TryParsePlaybackMediaOwnerId(
        string ownerId,
        out string folderId,
        out string itemId)
    {
        var separatorIndex = ownerId.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= ownerId.Length - 1)
        {
            folderId = string.Empty;
            itemId = string.Empty;
            return false;
        }

        folderId = ownerId[..separatorIndex];
        itemId = ownerId[(separatorIndex + 1)..];
        return true;
    }
}
