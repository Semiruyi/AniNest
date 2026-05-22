using AniNest.Contracts.Library;
using AniNest.Core.Enums;

namespace AniNest.Application.Library;

public sealed class LibraryCatalogService
{
    private readonly ILibraryCatalogStore _store;
    private readonly ILibraryFileScanner _scanner;

    public LibraryCatalogService(ILibraryCatalogStore store, ILibraryFileScanner scanner)
    {
        _store = store;
        _scanner = scanner;
    }

    public async Task<IReadOnlyList<LibraryFolderDto>> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        var orderedFolders = _store.GetFolders()
            .OrderBy(folder => folder.Order)
            .ThenBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rescanned = new List<LibraryFolderRecord>(orderedFolders.Count);
        var changed = false;

        foreach (var folder in orderedFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(folder.Path))
            {
                changed = true;
                continue;
            }

            var scanResult = await _scanner.ScanFolderAsync(folder.Path, cancellationToken);
            var updatedFolder = folder with
            {
                VideoCount = scanResult.VideoCount,
                CoverPath = scanResult.CoverPath
            };

            if (updatedFolder != folder)
                changed = true;

            rescanned.Add(updatedFolder);
        }

        var normalized = rescanned
            .Select((folder, index) => folder with { Order = index })
            .ToArray();

        if (changed)
            _store.SaveFolders(normalized);

        return normalized.Select(MapFolder).ToArray();
    }

    public async Task AddFolderAsync(AddLibraryFolderRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Path))
            throw new ArgumentException("Library folder path is required.", nameof(request));
        if (!Directory.Exists(request.Path))
            throw new ArgumentException($"Library folder '{request.Path}' does not exist.", nameof(request));

        var folders = _store.GetFolders().ToList();
        var folderId = CreateFolderId(request.Path);
        if (folders.Any(folder => string.Equals(folder.FolderId, folderId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Library folder '{request.Path}' has already been added.");

        var scanResult = await _scanner.ScanFolderAsync(request.Path, cancellationToken);
        if (scanResult.VideoCount == 0)
            throw new ArgumentException($"Library folder '{request.Path}' does not contain any supported video files.", nameof(request));

        var order = folders.Count == 0 ? 0 : folders.Max(folder => folder.Order) + 1;
        var name = Path.GetFileName(request.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        folders.Add(new LibraryFolderRecord(
            folderId,
            string.IsNullOrWhiteSpace(name) ? request.Path : name,
            request.Path,
            scanResult.VideoCount,
            scanResult.CoverPath,
            null,
            order));

        _store.SaveFolders(folders);
    }

    public async Task AddFolderBatchAsync(BatchAddLibraryFoldersRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.RootPath))
            throw new ArgumentException("Library root path is required.", nameof(request));
        if (!Directory.Exists(request.RootPath))
            throw new ArgumentException($"Library root path '{request.RootPath}' does not exist.", nameof(request));

        var folders = _store.GetFolders().ToList();
        var knownFolderIds = folders
            .Select(folder => folder.FolderId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var discoveredFolders = await _scanner.FindVideoFoldersAsync(request.RootPath, cancellationToken);
        foreach (var path in discoveredFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var folderId = CreateFolderId(path);
            if (knownFolderIds.Contains(folderId))
                continue;

            var scanResult = await _scanner.ScanFolderAsync(path, cancellationToken);
            if (scanResult.VideoCount == 0)
                continue;

            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var order = folders.Count == 0 ? 0 : folders.Max(folder => folder.Order) + 1;

            folders.Add(new LibraryFolderRecord(
                folderId,
                string.IsNullOrWhiteSpace(name) ? path : name,
                path,
                scanResult.VideoCount,
                scanResult.CoverPath,
                null,
                order));
            knownFolderIds.Add(folderId);
        }

        _store.SaveFolders(folders);
    }

    public void DeleteFolder(string folderId)
    {
        var updated = _store.GetFolders()
            .Where(folder => !string.Equals(folder.FolderId, folderId, StringComparison.OrdinalIgnoreCase))
            .Select((folder, index) => folder with { Order = index })
            .ToArray();

        _store.SaveFolders(updated);
    }

    public void SetFavorite(string folderId, bool isFavorite)
    {
        EnsureFolderExists(folderId);
        _store.SetIsFavorite(folderId, isFavorite);
    }

    public void SetWatchStatus(string folderId, WatchStatus status)
    {
        EnsureFolderExists(folderId);
        _store.SetWatchStatus(folderId, status);
    }

    public void MoveFolderToFront(string folderId)
    {
        var folders = _store.GetFolders().ToList();
        var target = folders.FirstOrDefault(folder => string.Equals(folder.FolderId, folderId, StringComparison.OrdinalIgnoreCase));
        if (target is null)
            throw new KeyNotFoundException($"Library folder '{folderId}' was not found.");

        folders.Remove(target);
        folders.Insert(0, target);
        _store.SaveFolders(folders.Select((folder, index) => folder with { Order = index }).ToArray());
    }

    private LibraryFolderDto MapFolder(LibraryFolderRecord folder)
    {
        var watchStatus = _store.GetWatchStatus(folder.FolderId);
        var isFavorite = _store.GetIsFavorite(folder.FolderId);

        return new LibraryFolderDto(
            folder.FolderId,
            folder.Name,
            folder.Path,
            folder.VideoCount,
            folder.CoverPath,
            0,
            watchStatus,
            isFavorite,
            folder.MetadataSummary);
    }

    private void EnsureFolderExists(string folderId)
    {
        if (_store.GetFolders().All(folder => !string.Equals(folder.FolderId, folderId, StringComparison.OrdinalIgnoreCase)))
            throw new KeyNotFoundException($"Library folder '{folderId}' was not found.");
    }

    private static string CreateFolderId(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(name))
            name = path;

        return name.Trim().ToLowerInvariant().Replace(' ', '-');
    }
}
