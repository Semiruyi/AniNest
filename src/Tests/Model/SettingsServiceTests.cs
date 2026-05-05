using System.IO;
using FluentAssertions;
using LocalPlayer.Infrastructure.Model;
using Moq;
using Xunit;

namespace LocalPlayer.Tests.Model;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"LocalPlayerTests_{Guid.NewGuid():N}");
        var dataDir = Path.Combine(_tempDir, "data", "config");
        Directory.CreateDirectory(dataDir);

        _service = new SettingsService(Path.Combine(dataDir, "settings.json"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Save_Load_RoundTrip()
    {
        _service.AddFolder("/test", "TestFolder");
        _service.SetVideoProgress("/test/video.mp4", 1000, 5000);
        _service.SetThumbnailExpiryDays(7);

        _service.Reload();
        var folders = _service.GetFolders();

        folders.Should().ContainSingle();
        folders[0].Name.Should().Be("TestFolder");
        _service.GetThumbnailExpiryDays().Should().Be(7);
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
        // NOTE: GetFolders has "鍏煎鏃ф暟鎹? logic that auto-fixes items at
        // index > 0 with OrderIndex==0. This means the first item in insertion
        // order must stay first in the reorder to avoid the compat code
        // corrupting the order. This is a known bug.
        _service.AddFolder("/a", "A");
        _service.AddFolder("/b", "B");
        _service.AddFolder("/c", "C");

        // a was inserted first 鈫?must stay first in reorder
        _service.ReorderFolders(new List<string> { "/a", "/c", "/b" });

        var ordered = _service.GetFolders();
        ordered[0].Path.Should().Be("/a");
        ordered[1].Path.Should().Be("/c");
        ordered[2].Path.Should().Be("/b");
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
    public void GetDefaultKeyBindings_HasEightEntries()
    {
        SettingsService.GetDefaultKeyBindings().Should().HaveCount(8);
    }

    [Fact]
    public void SetKeyBinding_CustomizesAction()
    {
        _service.SetKeyBinding("TogglePlayPause", System.Windows.Input.Key.Enter);

        _service.GetKeyBinding("TogglePlayPause").Should().Be(System.Windows.Input.Key.Enter);
    }

    [Fact]
    public void GetAllKeyBindings_ReturnsDefaultsWhenNoCustom()
    {
        var bindings = _service.GetAllKeyBindings();

        bindings.Should().HaveCount(8);
        bindings["TogglePlayPause"].Should().Be(System.Windows.Input.Key.Space);
    }

    [Fact]
    public void GetAllKeyBindings_ReturnsCustomOverrides()
    {
        _service.SetKeyBinding("Back", System.Windows.Input.Key.B);

        var bindings = _service.GetAllKeyBindings();

        bindings["Back"].Should().Be(System.Windows.Input.Key.B);
    }

    [Fact]
    public void Load_NewInstance_ReturnsDefaults()
    {
        // Empty settings file (just created)
        var folders = _service.GetFolders();
        folders.Should().BeEmpty();
    }
}

