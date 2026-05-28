using AniNest.Application.Library;
using AniNest.Application.Metadata;
using AniNest.Application.Playback;
using AniNest.Contracts.Library;

namespace AniNest.Host.Modules;

internal sealed class LibraryFolderProjection
{
    private readonly LibraryCatalogService _catalog;
    private readonly ILibraryFileScanner _scanner;
    private readonly IMetadataRuntimeBootstrapService _metadataBootstrap;
    private readonly IMetadataRuntimeStateService _metadataState;

    public LibraryFolderProjection(
        LibraryCatalogService catalog,
        ILibraryFileScanner scanner,
        IMetadataRuntimeBootstrapService metadataBootstrap,
        IMetadataRuntimeStateService metadataState)
    {
        _catalog = catalog;
        _scanner = scanner;
        _metadataBootstrap = metadataBootstrap;
        _metadataState = metadataState;
    }

    public async Task<IReadOnlyList<LibraryFolderSnapshot>> LoadFolderSnapshotsAsync(
        CancellationToken cancellationToken)
    {
        var folders = await _catalog.GetFoldersAsync(cancellationToken);
        var result = new List<LibraryFolderSnapshot>(folders.Count);
        foreach (var folder in folders)
        {
            result.Add(await CreateSnapshotAsync(folder, cancellationToken));
        }

        return result;
    }

    internal LibraryFolderDto ApplyMetadataSummaryForModule(LibraryFolderDto folder)
    {
        _metadataBootstrap.EnsureInitialized();
        var metadata = _metadataState.GetMetadata(folder.FolderId);
        var summary = _metadataState.GetFolderStateSummary(folder.FolderId);
        return _catalog.ApplyMetadataSummary(
            folder,
            metadata?.Title ?? summary.Title,
            metadata?.OriginalTitle,
            summary.PosterPath,
            summary.State.ToString(),
            summary.HasMetadata);
    }

    private async Task<LibraryFolderSnapshot> CreateSnapshotAsync(
        LibraryFolderDto folder,
        CancellationToken cancellationToken)
    {
        var record = _catalog.GetFolderRecord(folder.FolderId);
        if (record is null)
        {
            return new LibraryFolderSnapshot(
                folder,
                string.Empty,
                folder.Name,
                null,
                [],
                folder.VideoCount);
        }

        var videoFiles = await _scanner.GetVideoFilesAsync(record.Path, cancellationToken);
        var parentName = Path.GetDirectoryName(record.Path) is { } parentPath
            ? Path.GetFileName(parentPath)
            : null;

        return new LibraryFolderSnapshot(
            folder,
            record.Path,
            record.Name,
            parentName,
            videoFiles,
            folder.VideoCount);
    }
}
