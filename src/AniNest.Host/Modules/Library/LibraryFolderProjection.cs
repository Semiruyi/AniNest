using AniNest.Application.Library;
using AniNest.Application.Playback;
using AniNest.Contracts.Library;

namespace AniNest.Host.Modules;

internal sealed class LibraryFolderProjection
{
    private readonly LibraryCatalogService _catalog;
    private readonly ILibraryFileScanner _scanner;

    public LibraryFolderProjection(
        LibraryCatalogService catalog,
        ILibraryFileScanner scanner)
    {
        _catalog = catalog;
        _scanner = scanner;
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
