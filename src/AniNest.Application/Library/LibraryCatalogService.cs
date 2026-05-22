using AniNest.Contracts.Library;
using AniNest.Core.Enums;

namespace AniNest.Application.Library;

public sealed class LibraryCatalogService
{
    private readonly ILibraryCatalogStore _store;

    public LibraryCatalogService(ILibraryCatalogStore store)
    {
        _store = store;
    }

    public IReadOnlyList<LibraryFolderDto> GetFolders()
        => _store.GetFolders()
            .OrderBy(folder => folder.Order)
            .ThenBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .Select(MapFolder)
            .ToArray();

    public void AddFolder(AddLibraryFolderRequest request)
    {
        var folders = _store.GetFolders().ToList();
        var folderId = CreateFolderId(request.Path);
        if (folders.Any(folder => string.Equals(folder.FolderId, folderId, StringComparison.OrdinalIgnoreCase)))
            return;

        var order = folders.Count == 0 ? 0 : folders.Max(folder => folder.Order) + 1;
        var name = Path.GetFileName(request.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        folders.Add(new LibraryFolderRecord(
            folderId,
            string.IsNullOrWhiteSpace(name) ? request.Path : name,
            request.Path,
            0,
            null,
            null,
            order));

        _store.SaveFolders(folders);
    }

    public void AddFolderBatch(BatchAddLibraryFoldersRequest request)
    {
        AddFolder(new AddLibraryFolderRequest(Path.Combine(request.RootPath, "Imported-01")));
        AddFolder(new AddLibraryFolderRequest(Path.Combine(request.RootPath, "Imported-02")));
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
