using AniNest.Features.Metadata;
using FluentAssertions;
using Moq;

namespace AniNest.Tests.View;

public class MetadataQueryServiceTests
{
    [Fact]
    public void GetState_ReturnsNeedsMetadata_WhenRecordMissing()
    {
        var repository = new Mock<IMetadataRepository>();
        var indexStore = new MetadataIndexStore(Path.Combine(Path.GetTempPath(), $"MetadataQueryTests_{Guid.NewGuid():N}", "index.json"));
        var events = new MetadataEventHub();
        var service = new MetadataQueryService(repository.Object, indexStore, events);

        var state = service.GetState("/missing");

        state.Should().Be(MetadataState.NeedsMetadata);
    }

    [Fact]
    public void GetSummary_CountsStatesAndFailures()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"MetadataQueryTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var indexStore = new MetadataIndexStore(Path.Combine(directory, "index.json"));
        indexStore.Save(new Dictionary<string, MetadataRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["/a"] = new() { FolderPath = "/a", State = MetadataState.Ready },
            ["/b"] = new() { FolderPath = "/b", State = MetadataState.NeedsReview, FailureKind = MetadataFailureKind.NoMatch },
            ["/c"] = new() { FolderPath = "/c", State = MetadataState.Scraping, FailureKind = MetadataFailureKind.NetworkError }
        });

        var repository = new Mock<IMetadataRepository>();
        var events = new MetadataEventHub();
        var service = new MetadataQueryService(repository.Object, indexStore, events);

        var summary = service.GetSummary();

        summary.ReadyCount.Should().Be(1);
        summary.NeedsReviewCount.Should().Be(1);
        summary.ScrapingCount.Should().Be(1);
        summary.NoMatchCount.Should().Be(1);
        summary.NetworkErrorCount.Should().Be(1);

        Directory.Delete(directory, true);
    }
}
