using AniNest.Features.Metadata;
using FluentAssertions;

namespace AniNest.Tests.View;

public class MetadataIndexStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsRecords()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"MetadataIndexStoreTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var indexPath = Path.Combine(directory, "index.json");
        var store = new MetadataIndexStore(indexPath);

        var records = new Dictionary<string, MetadataRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["/folder"] = new()
            {
                FolderPath = "/folder",
                FolderName = "Folder",
                FolderFingerprint = "abc123",
                State = MetadataState.Ready,
                FailureKind = MetadataFailureKind.None,
                SourceId = "42"
            }
        };

        store.Save(records);

        var loaded = store.Load();

        loaded.Should().ContainKey("/folder");
        loaded["/folder"].FolderName.Should().Be("Folder");
        loaded["/folder"].State.Should().Be(MetadataState.Ready);
        loaded["/folder"].SourceId.Should().Be("42");

        Directory.Delete(directory, true);
    }
}
