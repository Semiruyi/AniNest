using AniNest.Application.Library;
using AniNest.Contracts.Library;
using AniNest.Core.Enums;

namespace AniNest.Backend.Tests;

public sealed class LibraryCatalogServiceTests
{
    [Fact]
    public async Task AddFolder_AppendsNewFolder()
    {
        var store = CreateStore(out var root);
        var scanner = new FakeLibraryFileScanner();
        var path = CreateChildFolder(root, "Bocchi The Rock");
        scanner.ScanResults[path] = new LibraryFolderScanResult(12, Path.Combine(path, "poster.jpg"));
        var service = new LibraryCatalogService(store, scanner);

        var result = await service.AddFolderAsync(new AddLibraryFolderRequest(path));

        Assert.Equal("added", result.Status);
        Assert.Null(result.ReasonCode);
        Assert.NotNull(result.Folder);
        var folders = await service.GetFoldersAsync();
        Assert.Equal(3, folders.Count);
        var folder = Assert.Single(folders, item => item.FolderId == "bocchi-the-rock");
        Assert.Equal(12, folder.VideoCount);
        Assert.Equal(Path.Combine(path, "poster.jpg"), folder.CoverPath);
    }

    [Fact]
    public async Task AddFolder_RejectsFoldersWithoutVideos()
    {
        var store = CreateStore(out var root);
        var scanner = new FakeLibraryFileScanner();
        var path = CreateChildFolder(root, "Empty Folder");
        scanner.ScanResults[path] = new LibraryFolderScanResult(0, null);
        var service = new LibraryCatalogService(store, scanner);

        var result = await service.AddFolderAsync(new AddLibraryFolderRequest(path));
        Assert.Equal("failed", result.Status);
        Assert.Equal("no_supported_videos", result.ReasonCode);
        Assert.Contains("does not contain any supported video files", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddFolder_ReturnsAlreadyExists_ForDuplicateFolder()
    {
        var store = CreateStore(out var root);
        var scanner = new FakeLibraryFileScanner();
        var path = Path.Combine(root, "Folder 01");
        var service = new LibraryCatalogService(store, scanner);

        var result = await service.AddFolderAsync(new AddLibraryFolderRequest(path));

        Assert.Equal("alreadyExists", result.Status);
        Assert.Equal("already_exists", result.ReasonCode);
        Assert.NotNull(result.Folder);
    }

    [Fact]
    public async Task AddFolderBatch_AddsOnlyValidNewFolders()
    {
        var store = CreateStore(out var root);
        var scanner = new FakeLibraryFileScanner();
        var importRoot = CreateChildFolder(root, "Import Root");
        var folderA = CreateChildFolder(importRoot, "Season A");
        var folderB = CreateChildFolder(importRoot, "Season B");
        var duplicate = CreateChildFolder(importRoot, "Folder 01");

        scanner.BatchResults[importRoot] = [folderA, folderB, duplicate];
        scanner.ScanResults[folderA] = new LibraryFolderScanResult(10, null);
        scanner.ScanResults[folderB] = new LibraryFolderScanResult(8, null);
        scanner.ScanResults[duplicate] = new LibraryFolderScanResult(6, null);

        var service = new LibraryCatalogService(store, scanner);

        await service.AddFolderBatchAsync(new BatchAddLibraryFoldersRequest(importRoot));

        var folders = await service.GetFoldersAsync();
        Assert.Equal(4, folders.Count);
        Assert.Contains(folders, folder => folder.FolderId == "season-a");
        Assert.Contains(folders, folder => folder.FolderId == "season-b");
        Assert.Single(folders, folder => folder.FolderId == "folder-01");
    }

    [Fact]
    public async Task GetFolders_RemovesMissingFoldersAndRefreshesScanData()
    {
        var existingPath = CreateExistingFolder("Folder 01");
        var missingPath = Path.Combine(Path.GetTempPath(), $"AniNest.Missing.{Guid.NewGuid():N}");
        var store = new InMemoryLibraryCatalogStore(
        [
            new LibraryFolderRecord("folder-01", "Folder 01", existingPath, 1, null, null, 0),
            new LibraryFolderRecord("folder-02", "Folder 02", missingPath, 24, null, null, 1)
        ]);
        var scanner = new FakeLibraryFileScanner();
        scanner.ScanResults[existingPath] = new LibraryFolderScanResult(15, Path.Combine(existingPath, "folder.jpg"));
        var service = new LibraryCatalogService(store, scanner);

        var folders = await service.GetFoldersAsync();

        Assert.Single(folders);
        Assert.Equal("folder-01", folders[0].FolderId);
        Assert.Equal(15, folders[0].VideoCount);
        Assert.Equal(Path.Combine(existingPath, "folder.jpg"), folders[0].CoverPath);
        Assert.Single(store.GetFolders());
    }

    [Fact]
    public async Task SetFavorite_UpdatesFolderState()
    {
        var store = CreateStore(out _);
        var service = new LibraryCatalogService(store, new FakeLibraryFileScanner());

        service.SetFavorite("folder-02", true);

        var folder = (await service.GetFoldersAsync()).Single(item => item.FolderId == "folder-02");
        Assert.True(folder.IsFavorite);
    }

    [Fact]
    public async Task SetWatchStatus_UpdatesFolderState()
    {
        var store = CreateStore(out _);
        var service = new LibraryCatalogService(store, new FakeLibraryFileScanner());

        service.SetWatchStatus("folder-02", WatchStatus.Completed);

        var folder = (await service.GetFoldersAsync()).Single(item => item.FolderId == "folder-02");
        Assert.Equal(WatchStatus.Completed, folder.WatchStatus);
    }

    [Fact]
    public async Task MoveFolderToFront_ReordersFolders()
    {
        var store = CreateStore(out _);
        var service = new LibraryCatalogService(store, new FakeLibraryFileScanner());

        service.MoveFolderToFront("folder-02");

        var folders = await service.GetFoldersAsync();
        Assert.Equal("folder-02", folders[0].FolderId);
    }

    [Fact]
    public async Task DeleteFolder_RemovesAndReordersRemainingFolders()
    {
        var store = CreateStore(out _);
        var service = new LibraryCatalogService(store, new FakeLibraryFileScanner());

        service.DeleteFolder("folder-01");

        var folders = await service.GetFoldersAsync();
        Assert.Single(folders);
        Assert.Equal("folder-02", folders[0].FolderId);
    }

    private static InMemoryLibraryCatalogStore CreateStore()
        => CreateStore(out _);

    private static InMemoryLibraryCatalogStore CreateStore(out string rootPath)
    {
        rootPath = Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"library-{Guid.NewGuid():N}");
        var folder01 = Path.Combine(rootPath, "Folder 01");
        var folder02 = Path.Combine(rootPath, "Folder 02");
        Directory.CreateDirectory(folder01);
        Directory.CreateDirectory(folder02);

        return new InMemoryLibraryCatalogStore(
        [
            new LibraryFolderRecord("folder-01", "Folder 01", folder01, 12, null, null, 0),
            new LibraryFolderRecord("folder-02", "Folder 02", folder02, 24, null, null, 1)
        ]);
    }

    private static string CreateExistingFolder(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"{name}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateChildFolder(string parentPath, string name)
    {
        var path = Path.Combine(parentPath, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
