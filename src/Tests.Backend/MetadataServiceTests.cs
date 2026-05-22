using AniNest.Application.Metadata;
using AniNest.Contracts.Metadata;
using AniNest.Core.Enums;

namespace AniNest.Backend.Tests;

public sealed class MetadataServiceTests
{
    [Fact]
    public void GetSummary_CountsStatesAndFailures()
    {
        var service = CreateService(
        [
            new MetadataDto("a", "A", null, null, [], null, null, null, null, MetadataState.Ready, MetadataFailureKind.None),
            new MetadataDto("b", "B", null, null, [], null, null, null, null, MetadataState.NeedsMetadata, MetadataFailureKind.None),
            new MetadataDto("c", "C", null, null, [], null, null, null, null, MetadataState.Disabled, MetadataFailureKind.NoMatch)
        ]);

        var summary = service.GetSummary();

        Assert.Equal(1, summary.Ready);
        Assert.Equal(1, summary.NeedsMetadata);
        Assert.Equal(1, summary.Disabled);
        Assert.Equal(1, summary.NoMatch);
    }

    [Fact]
    public void RefreshFolder_MovesItemToQueuedAndClearsFailure()
    {
        var store = new InMemoryMetadataStore(
        [
            new MetadataDto("a", "A", null, null, [], null, null, null, null, MetadataState.NeedsReview, MetadataFailureKind.NetworkError)
        ]);
        var service = new MetadataService(store);

        service.RefreshFolder("a");

        var item = store.GetByFolderId("a");
        Assert.NotNull(item);
        Assert.Equal(MetadataState.Queued, item.State);
        Assert.Equal(MetadataFailureKind.None, item.FailureKind);
    }

    [Fact]
    public void RetryFailed_SkipsNoMatch_WhenFlagDisabled()
    {
        var store = new InMemoryMetadataStore(
        [
            new MetadataDto("a", "A", null, null, [], null, null, null, null, MetadataState.NeedsReview, MetadataFailureKind.NoMatch)
        ]);
        var service = new MetadataService(store);

        service.RetryFailed(includeNoMatch: false);

        var item = store.GetByFolderId("a");
        Assert.NotNull(item);
        Assert.Equal(MetadataState.NeedsReview, item.State);
        Assert.Equal(MetadataFailureKind.NoMatch, item.FailureKind);
    }

    private static MetadataService CreateService(IReadOnlyList<MetadataDto> items)
        => new(new InMemoryMetadataStore(items));
}
