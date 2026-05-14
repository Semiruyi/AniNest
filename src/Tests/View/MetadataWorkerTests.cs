using AniNest.Features.Metadata;
using FluentAssertions;
using Moq;

namespace AniNest.Tests.View;

public class MetadataWorkerTests
{
    [Fact]
    public void Start_ProcessesQueuedRecord_AndRaisesRefreshEvent()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"MetadataWorkerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var indexPath = Path.Combine(directory, "index.json");

        var indexStore = new MetadataIndexStore(indexPath);
        indexStore.Save(new Dictionary<string, MetadataRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["/folder"] = new()
            {
                FolderPath = "/folder",
                FolderName = "Folder",
                State = MetadataState.Queued
            }
        });

        var taskStore = new MetadataTaskStore();
        taskStore.Enqueue("/folder");

        var repository = new Mock<IMetadataRepository>();
        FolderMetadata? savedMetadata = null;
        repository.Setup(service => service.Save(It.IsAny<FolderMetadata>()))
            .Callback<FolderMetadata>(metadata => savedMetadata = metadata);

        var imageCache = new Mock<IMetadataImageCache>();
        imageCache.Setup(cache => cache.CachePosterAsync("/folder", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Path.Combine(directory, "poster.jpg"));

        var provider = new Mock<IMetadataProvider>();
        provider.Setup(service => service.FetchAsync(It.IsAny<MetadataFolderRef>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MetadataFetchResult.Success(new FolderMetadata
            {
                FolderPath = "/folder",
                Title = "Title",
                PosterUrl = "https://example.com/poster.jpg",
                SourceId = "123",
                ScrapedAt = DateTime.UtcNow
            }));

        var events = new MetadataEventHub();
        using var refreshed = new ManualResetEventSlim(false);
        events.FolderMetadataRefreshed += (_, args) =>
        {
            if (args.FolderPath == "/folder")
                refreshed.Set();
        };

        using (var worker = new MetadataWorker(taskStore, indexStore, repository.Object, imageCache.Object, provider.Object, events))
        {
            worker.Start();

            refreshed.Wait(TimeSpan.FromSeconds(3)).Should().BeTrue();

            var loaded = indexStore.Load();
            loaded["/folder"].State.Should().Be(MetadataState.Ready);
            loaded["/folder"].FailureKind.Should().Be(MetadataFailureKind.None);
            loaded["/folder"].SourceId.Should().Be("123");
            savedMetadata.Should().NotBeNull();
            savedMetadata!.LocalPosterPath.Should().Be(Path.Combine(directory, "poster.jpg"));
        }

        Directory.Delete(directory, true);
    }
}
