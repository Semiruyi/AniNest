using AniNest.Application.Library;
using AniNest.Application.Metadata;
using AniNest.Application.Resources;

namespace AniNest.Host.Modules.Resources;

internal sealed class ResourceLocator : IResourceLocator
{
    private readonly ILibraryCatalogStore _libraryCatalogStore;
    private readonly ILibraryFileScanner _libraryFileScanner;
    private readonly IMetadataStore _metadataStore;

    public ResourceLocator(
        ILibraryCatalogStore libraryCatalogStore,
        ILibraryFileScanner libraryFileScanner,
        IMetadataStore metadataStore)
    {
        _libraryCatalogStore = libraryCatalogStore;
        _libraryFileScanner = libraryFileScanner;
        _metadataStore = metadataStore;
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

        return folder.CoverPath;
    }

    private string? ResolveLibraryPosterPath(string folderId)
    {
        var metadata = _metadataStore.GetByFolderId(folderId);
        if (!string.IsNullOrWhiteSpace(metadata?.PosterPath))
        {
            return metadata!.PosterPath;
        }

        return _libraryCatalogStore.GetFolders()
            .FirstOrDefault(folder =>
                string.Equals(folder.FolderId, folderId, StringComparison.OrdinalIgnoreCase))
            ?.MetadataSummary?.PosterPath;
    }
}
