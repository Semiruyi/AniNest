using AniNest.Application.Library;
using AniNest.Application.Metadata;
using AniNest.Application.Resources;

namespace AniNest.Host.Modules.Resources;

internal sealed class ResourceLocator : IResourceLocator
{
    private readonly ILibraryCatalogStore _libraryCatalogStore;
    private readonly ILibraryFileScanner _libraryFileScanner;
    private readonly IMetadataStore _metadataStore;
    private readonly string _metadataPosterRootPath;

    public ResourceLocator(
        ILibraryCatalogStore libraryCatalogStore,
        ILibraryFileScanner libraryFileScanner,
        IMetadataStore metadataStore,
        string metadataPosterRootPath)
    {
        _libraryCatalogStore = libraryCatalogStore;
        _libraryFileScanner = libraryFileScanner;
        _metadataStore = metadataStore;
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

    private string? ResolveMetadataPosterPath(string? posterPath)
    {
        if (string.IsNullOrWhiteSpace(posterPath))
            return null;

        if (Path.IsPathRooted(posterPath))
            return posterPath;

        return Path.Combine(_metadataPosterRootPath, posterPath);
    }
}
