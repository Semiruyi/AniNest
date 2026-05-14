using AniNest.Features.Metadata;
using AniNest.Infrastructure.Persistence;
using FluentAssertions;
using Moq;

namespace AniNest.Tests.View;

public class MetadataSyncCoordinatorTests
{
    [Fact]
    public async Task SyncLibrarySnapshotAsync_CreatesMissingRecords_AndRemovesOrphans()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"MetadataSyncCoordinatorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var indexPath = Path.Combine(directory, "index.json");
        var settingsPath = Path.Combine(directory, "settings.json");

        var indexStore = new MetadataIndexStore(indexPath);
        indexStore.Save(new Dictionary<string, MetadataRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["/orphan"] = new()
            {
                FolderPath = "/orphan",
                FolderName = "Orphan",
                State = MetadataState.Ready
            }
        });

        var repository = new Mock<IMetadataRepository>();
        var settings = new SettingsService(settingsPath);
        var taskStore = new MetadataTaskStore();
        var events = new MetadataEventHub();
        var coordinator = new MetadataSyncCoordinator(indexStore, repository.Object, settings, taskStore, events);

        await coordinator.SyncLibrarySnapshotAsync(
        [
            new MetadataFolderRef("/folder", "Folder", ["/folder/a.mp4"])
        ]);

        var loaded = indexStore.Load();

        loaded.Should().ContainKey("/folder");
        loaded["/folder"].State.Should().Be(MetadataState.Queued);
        loaded.Should().NotContainKey("/orphan");
        repository.Verify(service => service.Delete("/orphan"), Times.Once);

        settings.Dispose();
        Directory.Delete(directory, true);
    }

    [Fact]
    public async Task SyncLibrarySnapshotAsync_NormalizesStaleRuntimeStates()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"MetadataSyncCoordinatorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var indexPath = Path.Combine(directory, "index.json");
        var settingsPath = Path.Combine(directory, "settings.json");

        var indexStore = new MetadataIndexStore(indexPath);
        indexStore.Save(new Dictionary<string, MetadataRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["/folder"] = new()
            {
                FolderPath = "/folder",
                FolderName = "Folder",
                State = MetadataState.Scraping
            }
        });

        var repository = new Mock<IMetadataRepository>();
        var settings = new SettingsService(settingsPath);
        var taskStore = new MetadataTaskStore();
        var events = new MetadataEventHub();
        var coordinator = new MetadataSyncCoordinator(indexStore, repository.Object, settings, taskStore, events);

        await coordinator.SyncLibrarySnapshotAsync(
        [
            new MetadataFolderRef("/folder", "Folder", ["/folder/a.mp4"])
        ]);

        var loaded = indexStore.Load();
        loaded["/folder"].State.Should().Be(MetadataState.Queued);

        settings.Dispose();
        Directory.Delete(directory, true);
    }

    [Fact]
    public async Task SyncLibrarySnapshotAsync_ResetsRecord_WhenFingerprintChanges()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"MetadataSyncCoordinatorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var indexPath = Path.Combine(directory, "index.json");
        var settingsPath = Path.Combine(directory, "settings.json");
        var metadataFilePath = Path.Combine(directory, "folder.metadata.json");
        var posterFilePath = Path.Combine(directory, "folder.poster.jpg");

        File.WriteAllText(metadataFilePath, "{}");
        File.WriteAllText(posterFilePath, "poster");

        var indexStore = new MetadataIndexStore(indexPath);
        indexStore.Save(new Dictionary<string, MetadataRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["/folder"] = new()
            {
                FolderPath = "/folder",
                FolderName = "Folder",
                FolderFingerprint = "oldfingerprint",
                State = MetadataState.Ready,
                FailureKind = MetadataFailureKind.ProviderError,
                SourceId = "bangumi:123",
                LastAttemptAtUtc = DateTime.UtcNow.AddDays(-2),
                LastSucceededAtUtc = DateTime.UtcNow.AddDays(-1),
                CooldownUntilUtc = DateTime.UtcNow.AddHours(3),
                MetadataFilePath = metadataFilePath,
                PosterFilePath = posterFilePath
            }
        });

        var repository = new Mock<IMetadataRepository>();
        var settings = new SettingsService(settingsPath);
        var taskStore = new MetadataTaskStore();
        var events = new MetadataEventHub();
        var coordinator = new MetadataSyncCoordinator(indexStore, repository.Object, settings, taskStore, events);

        await coordinator.SyncLibrarySnapshotAsync(
        [
            new MetadataFolderRef("/folder", "Folder", ["/folder/b.mp4", "/folder/c.mp4"])
        ]);

        var loaded = indexStore.Load();
        var record = loaded["/folder"];

        record.State.Should().Be(MetadataState.Queued);
        record.FailureKind.Should().Be(MetadataFailureKind.None);
        record.SourceId.Should().BeNull();
        record.LastAttemptAtUtc.Should().BeNull();
        record.LastSucceededAtUtc.Should().BeNull();
        record.CooldownUntilUtc.Should().BeNull();
        record.MetadataFilePath.Should().BeNull();
        record.PosterFilePath.Should().BeNull();
        File.Exists(metadataFilePath).Should().BeFalse();
        File.Exists(posterFilePath).Should().BeFalse();
        repository.Verify(service => service.Delete("/folder"), Times.Once);

        settings.Dispose();
        Directory.Delete(directory, true);
    }

    [Fact]
    public async Task RetryFailedAsync_RequeuesTransientFailures_Only()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"MetadataSyncCoordinatorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var indexPath = Path.Combine(directory, "index.json");
        var settingsPath = Path.Combine(directory, "settings.json");

        var indexStore = new MetadataIndexStore(indexPath);
        indexStore.Save(new Dictionary<string, MetadataRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["/network"] = new()
            {
                FolderPath = "/network",
                FolderName = "Network",
                State = MetadataState.NeedsMetadata,
                FailureKind = MetadataFailureKind.NetworkError,
                CooldownUntilUtc = DateTime.UtcNow.AddMinutes(10)
            },
            ["/nomatch"] = new()
            {
                FolderPath = "/nomatch",
                FolderName = "NoMatch",
                State = MetadataState.NeedsReview,
                FailureKind = MetadataFailureKind.NoMatch,
                CooldownUntilUtc = DateTime.UtcNow.AddDays(1)
            }
        });

        var repository = new Mock<IMetadataRepository>();
        var settings = new SettingsService(settingsPath);
        var taskStore = new MetadataTaskStore();
        var events = new MetadataEventHub();
        var coordinator = new MetadataSyncCoordinator(indexStore, repository.Object, settings, taskStore, events);

        await coordinator.RetryFailedAsync(includeNoMatch: false);

        var loaded = indexStore.Load();
        loaded["/network"].State.Should().Be(MetadataState.Queued);
        loaded["/network"].FailureKind.Should().Be(MetadataFailureKind.None);
        loaded["/network"].CooldownUntilUtc.Should().BeNull();
        loaded["/nomatch"].State.Should().Be(MetadataState.NeedsReview);
        loaded["/nomatch"].FailureKind.Should().Be(MetadataFailureKind.NoMatch);

        settings.Dispose();
        Directory.Delete(directory, true);
    }
}
