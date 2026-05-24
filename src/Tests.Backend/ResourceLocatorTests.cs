using AniNest.Application.Library;
using AniNest.Application.Resources;
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
        var metadataStore = new InMemoryMetadataStore(
        [
            new MetadataDto(
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
                AniNest.Core.Enums.MetadataFailureKind.None)
        ]);
        var locator = new ResourceLocator(store, scanner, metadataStore, root);

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
        var metadataStore = new InMemoryMetadataStore(
        [
            new MetadataDto(
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
                AniNest.Core.Enums.MetadataFailureKind.None)
        ]);
        var locator = new ResourceLocator(store, scanner, metadataStore, posterRoot);

        var resource = await locator.ResolveAsync(new ResourceKey(ResourceKind.LibraryPoster, "folder-01"));

        Assert.NotNull(resource);
        Assert.Equal(posterPath, resource!.FilePath);
        Assert.Equal("image/jpeg", resource.ContentType);
    }
}
