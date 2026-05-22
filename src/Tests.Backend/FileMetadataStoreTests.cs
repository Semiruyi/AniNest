using AniNest.Contracts.Metadata;
using AniNest.Core.Enums;
using AniNest.Host.Modules;

namespace AniNest.Backend.Tests;

public sealed class FileMetadataStoreTests
{
    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var store = new FileMetadataStore(CreateTempPath(), MetadataDefaults.Create());

        var all = store.GetAll();

        Assert.Single(all);
        Assert.Equal("sample-folder", all[0].FolderId);
    }

    [Fact]
    public void Save_PersistsMetadataToDisk()
    {
        var path = CreateTempPath();
        var store = new FileMetadataStore(path, MetadataDefaults.Create());

        store.Save(new MetadataDto(
            "folder-02",
            "Bocchi the Rock!",
            "Bocchi the Rock!",
            "Band story",
            ["music"],
            null,
            "S1",
            12,
            "local",
            MetadataState.Queued,
            MetadataFailureKind.None));

        var reloaded = new FileMetadataStore(path, MetadataDefaults.Create());
        var metadata = reloaded.GetByFolderId("folder-02");
        Assert.NotNull(metadata);
        Assert.Equal("Bocchi the Rock!", metadata.Title);
        Assert.Equal(MetadataState.Queued, metadata.State);
    }

    private static string CreateTempPath()
        => Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"{Guid.NewGuid():N}.metadata.json");
}
