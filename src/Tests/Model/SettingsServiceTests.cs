using System.IO;
using FluentAssertions;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Thumbnails;
using Xunit;

namespace AniNest.Tests.Model;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"AniNestTests_{Guid.NewGuid():N}");
        var dataDir = Path.Combine(_tempDir, "data", "config");
        Directory.CreateDirectory(dataDir);

        _settingsPath = Path.Combine(dataDir, "settings.json");
        _service = new SettingsService(_settingsPath);
    }

    public void Dispose()
    {
        _service.Dispose();

        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Save_Load_RoundTrip()
    {
        _service.AddFolder("/test", "TestFolder");
        _service.SetVideoProgress("/test/video.mp4", 1000, 5000);
        _service.SetThumbnailExpiryDays(7);
        _service.SetThumbnailPerformanceMode(ThumbnailPerformanceMode.Fast);
        _service.SetThumbnailGenerationPaused(true);
        _service.SetThumbnailAccelerationMode(ThumbnailAccelerationMode.Compatible);

        _service.Reload();
        var folders = _service.GetFolders();

        folders.Should().ContainSingle();
        folders[0].Name.Should().Be("TestFolder");
        _service.GetThumbnailExpiryDays().Should().Be(7);
        _service.GetThumbnailPerformanceMode().Should().Be(ThumbnailPerformanceMode.Fast);
        _service.IsThumbnailGenerationPaused().Should().BeTrue();
        _service.GetThumbnailAccelerationMode().Should().Be(ThumbnailAccelerationMode.Compatible);
    }

    [Fact]
    public void Save_DoesNotLeaveTemporaryFile()
    {
        _service.AddFolder("/test", "TestFolder");

        File.Exists($"{_settingsPath}.tmp").Should().BeFalse();
    }

    [Fact]
    public void Save_ExistingFile_ReplacesContent()
    {
        File.WriteAllText(_settingsPath, """
        {
          "ThumbnailExpiryDays": 3
        }
        """);

        _service.Reload();
        _service.SetThumbnailExpiryDays(9);

        var reloaded = new SettingsService(_settingsPath);
        reloaded.GetThumbnailExpiryDays().Should().Be(9);
        File.Exists($"{_settingsPath}.tmp").Should().BeFalse();
        reloaded.Dispose();
    }

    [Fact]
    public async Task DeferredSave_WritesUpdatedVideoProgressToDisk()
    {
        _service.SetVideoProgress("/video.mp4", 1200, 9000);

        await Task.Delay(1000);
        _service.Reload();

        var progress = _service.GetVideoProgress("/video.mp4");
        progress.Should().NotBeNull();
        progress!.Position.Should().Be(1200);
        progress.Duration.Should().Be(9000);
    }

    [Fact]
    public async Task DeferredSave_MultipleUpdates_PersistsLatestValue()
    {
        _service.SetVideoProgress("/video.mp4", 1000, 9000);
        _service.SetVideoProgress("/video.mp4", 2500, 9000);

        await Task.Delay(1000);
        _service.Reload();

        var progress = _service.GetVideoProgress("/video.mp4");
        progress.Should().NotBeNull();
        progress!.Position.Should().Be(2500);
    }

    [Fact]
    public void Dispose_FlushesPendingDeferredSave()
    {
        _service.SetFolderProgress("/folder", "/folder/episode-02.mp4");

        _service.Dispose();

        var reloaded = new SettingsService(_settingsPath);
        var progress = reloaded.GetFolderProgress("/folder");
        progress.Should().NotBeNull();
        progress!.LastVideoPath.Should().Be("/folder/episode-02.mp4");
        reloaded.Dispose();
    }

    [Fact]
    public void GetThumbnailPerformanceMode_DefaultsToBalanced()
    {
        _service.GetThumbnailPerformanceMode().Should().Be(ThumbnailPerformanceMode.Balanced);
    }

    [Fact]
    public void GetThumbnailAccelerationMode_DefaultsToAuto()
    {
        _service.GetThumbnailAccelerationMode().Should().Be(ThumbnailAccelerationMode.Auto);
    }

    [Fact]
    public void IsThumbnailGenerationPaused_DefaultsToFalse()
    {
        _service.IsThumbnailGenerationPaused().Should().BeFalse();
    }

    [Fact]
    public void AddFolder_NewFolder_Added()
    {
        _service.AddFolder("/movies", "Movies");

        _service.GetFolders().Should().ContainSingle()
            .Which.Name.Should().Be("Movies");
    }

    [Fact]
    public void AddFolder_DuplicatePath_NotAdded()
    {
        _service.AddFolder("/movies", "Movies");
        _service.AddFolder("/movies", "Movies Again");

        _service.GetFolders().Should().ContainSingle();
    }

    [Fact]
    public void RemoveFolder_RemovesCorrectly()
    {
        _service.AddFolder("/a", "A");
        _service.AddFolder("/b", "B");

        _service.RemoveFolder("/a");

        _service.GetFolders().Should().ContainSingle()
            .Which.Path.Should().Be("/b");
    }

    [Fact]
    public void ReorderFolders_UpdatesOrder()
    {
        _service.AddFolder("/a", "A");
        _service.AddFolder("/b", "B");
        _service.AddFolder("/c", "C");

        _service.ReorderFolders(new List<string> { "/a", "/c", "/b" });

        var ordered = _service.GetFolders();
        ordered[0].Path.Should().Be("/a");
        ordered[1].Path.Should().Be("/c");
        ordered[2].Path.Should().Be("/b");
    }

    [Fact]
    public void ReorderFolders_MoveToFront_PersistsAfterReload()
    {
        _service.AddFolder("/a", "A");
        _service.AddFolder("/b", "B");
        _service.AddFolder("/c", "C");

        _service.ReorderFolders(new List<string> { "/c", "/a", "/b" });

        _service.Reload();
        var ordered = _service.GetFolders();

        ordered.Select(f => f.Path).Should().Equal("/c", "/a", "/b");
    }

    [Fact]
    public void SetVideoProgress_SavesCorrectly()
    {
        _service.SetVideoProgress("/v.mp4", 3000, 9000);

        var progress = _service.GetVideoProgress("/v.mp4");
        progress.Should().NotBeNull();
        progress!.Position.Should().Be(3000);
        progress.Duration.Should().Be(9000);
        progress.IsPlayed.Should().BeTrue();
    }

    [Fact]
    public void GetVideoProgress_NotExist_ReturnsNull()
    {
        _service.GetVideoProgress("/nonexistent.mp4").Should().BeNull();
    }

    [Fact]
    public void MarkVideoPlayed_SetsFlag()
    {
        _service.MarkVideoPlayed("/v.mp4");

        _service.IsVideoPlayed("/v.mp4").Should().BeTrue();
    }

    [Fact]
    public void IsVideoPlayed_NewVideo_ReturnsFalse()
    {
        _service.IsVideoPlayed("/new.mp4").Should().BeFalse();
    }

    [Fact]
    public void SetFolderProgress_SavesCorrectly()
    {
        _service.SetFolderProgress("/folder", "/folder/last.mp4");

        var progress = _service.GetFolderProgress("/folder");
        progress.Should().NotBeNull();
        progress!.LastVideoPath.Should().Be("/folder/last.mp4");
    }

    [Fact]
    public void GetFolderProgress_NotExist_ReturnsNull()
    {
        _service.GetFolderProgress("/no-folder").Should().BeNull();
    }

    [Fact]
    public void GetFolderPlayedPercent_AllPlayed_Returns100()
    {
        var videos = new[] { "/f/a.mp4", "/f/b.mp4" };
        _service.MarkVideoPlayed("/f/a.mp4");
        _service.MarkVideoPlayed("/f/b.mp4");

        _service.GetFolderPlayedPercent("/f", videos).Should().Be(100);
    }

    [Fact]
    public void GetFolderPlayedPercent_HalfPlayed_Returns50()
    {
        var videos = new[] { "/f/a.mp4", "/f/b.mp4" };
        _service.MarkVideoPlayed("/f/a.mp4");

        _service.GetFolderPlayedPercent("/f", videos).Should().Be(50);
    }

    [Fact]
    public void GetFolderPlayedPercent_NonePlayed_Returns0()
    {
        var videos = new[] { "/f/a.mp4" };

        _service.GetFolderPlayedPercent("/f", videos).Should().Be(0);
    }

    [Fact]
    public void ClearFolderWatchHistory_RemovesVideoAndFolderProgressForFolder()
    {
        _service.MarkVideoPlayed("/f/a.mp4");
        _service.MarkVideoPlayed("/f/sub/b.mp4");
        _service.MarkVideoPlayed("/other/c.mp4");
        _service.SetFolderProgress("/f", "/f/a.mp4");

        _service.ClearFolderWatchHistory("/f");

        _service.IsVideoPlayed("/f/a.mp4").Should().BeFalse();
        _service.IsVideoPlayed("/f/sub/b.mp4").Should().BeFalse();
        _service.IsVideoPlayed("/other/c.mp4").Should().BeTrue();
        _service.GetFolderProgress("/f").Should().BeNull();
    }

    [Fact]
    public void ClearFolderWatchHistory_DoesNotMatchSiblingFolderPrefixes()
    {
        _service.MarkVideoPlayed("/folder/a.mp4");
        _service.MarkVideoPlayed("/folder-2/b.mp4");

        _service.ClearFolderWatchHistory("/folder");

        _service.IsVideoPlayed("/folder/a.mp4").Should().BeFalse();
        _service.IsVideoPlayed("/folder-2/b.mp4").Should().BeTrue();
    }

    [Fact]
    public void Load_NewInstance_ReturnsDefaults()
    {
        // Empty settings file (just created)
        var folders = _service.GetFolders();
        folders.Should().BeEmpty();
    }

    // ── AddFoldersBatch ──────────────────────────────────────────

    [Fact]
    public void AddFoldersBatch_AllNew_Added()
    {
        var (added, skipped) = _service.AddFoldersBatch(new()
        {
            ("/a", "A"),
            ("/b", "B"),
            ("/c", "C"),
        });

        added.Should().BeEquivalentTo(new[] { "/a", "/b", "/c" });
        skipped.Should().Be(0);
        _service.GetFolders().Should().HaveCount(3);
    }

    [Fact]
    public void AddFoldersBatch_PartialDuplicate_SkipsOnlyDuplicates()
    {
        _service.AddFolder("/a", "A");

        var (added, skipped) = _service.AddFoldersBatch(new()
        {
            ("/a", "A"),
            ("/b", "B"),
        });

        added.Should().BeEquivalentTo(new[] { "/b" });
        skipped.Should().Be(1);
        _service.GetFolders().Should().HaveCount(2);
    }

    [Fact]
    public void AddFoldersBatch_AllDuplicate_AddsNone()
    {
        _service.AddFolder("/a", "A");
        _service.AddFolder("/b", "B");

        var (added, skipped) = _service.AddFoldersBatch(new()
        {
            ("/a", "A"),
            ("/b", "B"),
        });

        added.Should().BeEmpty();
        skipped.Should().Be(2);
        _service.GetFolders().Should().HaveCount(2);
    }

    [Fact]
    public void AddFoldersBatch_EmptyList_ReturnsZeroes()
    {
        var (added, skipped) = _service.AddFoldersBatch(new());

        added.Should().BeEmpty();
        skipped.Should().Be(0);
    }

    [Fact]
    public void AddFoldersBatch_MixedWithExisting_OrderIndexContinues()
    {
        _service.AddFolder("/a", "A");
        _service.AddFolder("/b", "B");

        _service.AddFoldersBatch(new() { ("/c", "C") });

        var folders = _service.GetFolders();
        folders.Select(f => f.OrderIndex).Should().BeInAscendingOrder();
    }
}



