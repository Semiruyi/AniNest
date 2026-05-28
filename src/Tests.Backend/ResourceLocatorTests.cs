using AniNest.Application.Library;
using AniNest.Application.Playlist;
using AniNest.Application.Resources;
using AniNest.Contracts.Playlist;
using AniNest.Contracts.Metadata;
using AniNest.Host.Modules.Resources;

namespace AniNest.Backend.Tests;

public sealed class ResourceLocatorTests
{
    [Fact]
    public async Task ResolveAsync_ReturnsMetadataPosterForLibraryCover_WhenScannedCoverMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"resource-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var folderPath = Path.Combine(root, "Folder 01");
        Directory.CreateDirectory(folderPath);
        var posterPath = Path.Combine(root, "poster.jpg");
        await File.WriteAllBytesAsync(posterPath, [0xFF, 0xD8, 0xFF, 0xD9]);

        var store = new InMemoryLibraryCatalogStore(
        [
            new LibraryFolderRecord(
                "folder-01",
                "Folder 01",
                folderPath,
                12,
                null,
                new LibraryFolderMetadataSummary("Bocchi", posterPath, "Ready", true),
                0)
        ]);
        var scanner = new FakeLibraryFileScanner();
        scanner.ScanResults[folderPath] = new LibraryFolderScanResult(12, null);
        var metadataState = new FakeMetadataRuntimeStateService();
        metadataState.MetadataByFolderId["folder-01"] = new MetadataDto(
            "folder-01",
            "Bocchi",
            null,
            null,
            [],
            posterPath,
            null,
            12,
            "bangumi",
            AniNest.Core.Enums.MetadataState.Ready,
            AniNest.Core.Enums.MetadataFailureKind.None);
        var locator = new ResourceLocator(
            store,
            metadataState,
            new InMemoryPlaylistCatalogStore([]),
            root);

        var resource = await locator.ResolveAsync(new ResourceKey(ResourceKind.LibraryCover, "folder-01"));

        Assert.NotNull(resource);
        Assert.Equal(posterPath, resource!.FilePath);
        Assert.Equal("image/jpeg", resource.ContentType);
    }

    [Fact]
    public async Task ResolveAsync_CombinesPosterRoot_WhenMetadataPosterPathIsRelative()
    {
        var root = Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"resource-{Guid.NewGuid():N}");
        var posterRoot = Path.Combine(root, "metadata", "posters");
        Directory.CreateDirectory(posterRoot);
        var folderPath = Path.Combine(root, "Folder 01");
        Directory.CreateDirectory(folderPath);
        var posterFileName = "folder-01.jpg";
        var posterPath = Path.Combine(posterRoot, posterFileName);
        await File.WriteAllBytesAsync(posterPath, [0xFF, 0xD8, 0xFF, 0xD9]);

        var store = new InMemoryLibraryCatalogStore(
        [
            new LibraryFolderRecord(
                "folder-01",
                "Folder 01",
                folderPath,
                12,
                null,
                new LibraryFolderMetadataSummary("Bocchi", posterFileName, "Ready", true),
                0)
        ]);
        var scanner = new FakeLibraryFileScanner();
        scanner.ScanResults[folderPath] = new LibraryFolderScanResult(12, null);
        var metadataState = new FakeMetadataRuntimeStateService();
        metadataState.MetadataByFolderId["folder-01"] = new MetadataDto(
            "folder-01",
            "Bocchi",
            null,
            null,
            [],
            posterFileName,
            null,
            12,
            "bangumi",
            AniNest.Core.Enums.MetadataState.Ready,
            AniNest.Core.Enums.MetadataFailureKind.None);
        var locator = new ResourceLocator(
            store,
            metadataState,
            new InMemoryPlaylistCatalogStore([]),
            posterRoot);

        var resource = await locator.ResolveAsync(new ResourceKey(ResourceKind.LibraryPoster, "folder-01"));

        Assert.NotNull(resource);
        Assert.Equal(posterPath, resource!.FilePath);
        Assert.Equal("image/jpeg", resource.ContentType);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsPlaybackMedia_ForPlaylistItem()
    {
        var root = Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"resource-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var videoPath = Path.Combine(root, "Episode 01.mp4");
        await File.WriteAllBytesAsync(videoPath, []);

        var playlistStore = new InMemoryPlaylistCatalogStore(
        [
            new PlaylistDto(
                "folder-01",
                "Folder 01",
                "ep-01",
                0,
                [
                    new PlaylistItemDto(
                        "ep-01",
                        0,
                        "Episode 01",
                        videoPath,
                        false,
                        false,
                        0,
                        0,
                        AniNest.Core.Enums.ThumbnailState.Pending)
                ])
        ]);
        var locator = new ResourceLocator(
            new InMemoryLibraryCatalogStore([]),
            new FakeMetadataRuntimeStateService(),
            playlistStore,
            root);

        var resource = await locator.ResolveAsync(
            new ResourceKey(ResourceKind.PlaybackMedia, "folder-01:ep-01"));

        Assert.NotNull(resource);
        Assert.Equal(videoPath, resource!.FilePath);
        Assert.Equal("video/mp4", resource.ContentType);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsPlaybackMedia_ForMatchingFolderWhenItemIdsRepeat()
    {
        var root = Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"resource-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var firstVideoPath = Path.Combine(root, "FolderA-Episode01.mp4");
        var secondVideoPath = Path.Combine(root, "FolderB-Episode01.mp4");
        await File.WriteAllBytesAsync(firstVideoPath, []);
        await File.WriteAllBytesAsync(secondVideoPath, []);

        var playlistStore = new InMemoryPlaylistCatalogStore(
        [
            new PlaylistDto(
                "folder-a",
                "Folder A",
                "ep-01",
                0,
                [
                    new PlaylistItemDto(
                        "ep-01",
                        0,
                        "Episode 01",
                        firstVideoPath,
                        false,
                        false,
                        0,
                        0,
                        AniNest.Core.Enums.ThumbnailState.Pending)
                ]),
            new PlaylistDto(
                "folder-b",
                "Folder B",
                "ep-01",
                0,
                [
                    new PlaylistItemDto(
                        "ep-01",
                        0,
                        "Episode 01",
                        secondVideoPath,
                        false,
                        false,
                        0,
                        0,
                        AniNest.Core.Enums.ThumbnailState.Pending)
                ])
        ]);
        var locator = new ResourceLocator(
            new InMemoryLibraryCatalogStore([]),
            new FakeMetadataRuntimeStateService(),
            playlistStore,
            root);

        var resource = await locator.ResolveAsync(
            new ResourceKey(ResourceKind.PlaybackMedia, "folder-b:ep-01"));

        Assert.NotNull(resource);
        Assert.Equal(secondVideoPath, resource!.FilePath);
    }

    [Fact]
    public async Task ResolveAsync_DoesNotRescanOrMutateLibraryCatalog_ForLibraryCover()
    {
        var root = Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"resource-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var folderPath = Path.Combine(root, "Folder 01");
        Directory.CreateDirectory(folderPath);
        var existingCoverPath = Path.Combine(root, "existing-cover.jpg");
        await File.WriteAllBytesAsync(existingCoverPath, [0xFF, 0xD8, 0xFF, 0xD9]);

        var store = new InMemoryLibraryCatalogStore(
        [
            new LibraryFolderRecord(
                "folder-01",
                "Folder 01",
                folderPath,
                12,
                existingCoverPath,
                null,
                0)
        ]);
        var locator = new ResourceLocator(
            store,
            new FakeMetadataRuntimeStateService(),
            new InMemoryPlaylistCatalogStore([]),
            root);

        var resource = await locator.ResolveAsync(new ResourceKey(ResourceKind.LibraryCover, "folder-01"));

        Assert.NotNull(resource);
        Assert.Equal(existingCoverPath, resource!.FilePath);
        Assert.Equal(existingCoverPath, Assert.Single(store.GetFolders()).CoverPath);
    }
}
