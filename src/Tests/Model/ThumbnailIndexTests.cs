using FluentAssertions;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using Xunit;
using System.IO;

namespace LocalPlayer.Tests.Model;

public class ThumbnailIndexTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _thumbBaseDir;

    public ThumbnailIndexTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ThumbnailIndexTests_{Guid.NewGuid():N}");
        _thumbBaseDir = Path.Combine(_tempDir, "thumbs");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_thumbBaseDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Save_Load_RoundTrip()
    {
        var indexPath = Path.Combine(_tempDir, "index.json");
        var tasks = new List<ThumbnailTask>
        {
            new()
            {
                VideoPath = "/videos/test.mp4",
                Md5Dir = "abc123",
                State = ThumbnailState.Ready,
                TotalFrames = 60,
                Priority = 5
            }
        };

        // Create the disk directory so Load preserves Ready state
        var taskDir = Path.Combine(_thumbBaseDir, "abc123");
        Directory.CreateDirectory(taskDir);
        File.WriteAllText(Path.Combine(taskDir, "0001.jpg"), "");

        ThumbnailIndex.Save(indexPath, tasks);
        File.Exists(indexPath).Should().BeTrue();

        var loaded = ThumbnailIndex.Load(indexPath, _thumbBaseDir, new HashSet<string>());
        loaded.Should().ContainSingle();
        loaded[0].VideoPath.Should().Be("/videos/test.mp4");
        loaded[0].Md5Dir.Should().Be("abc123");
        loaded[0].TotalFrames.Should().Be(1); // 1 jpg file on disk
    }

    [Fact]
    public void Load_FileNotExist_ReturnsEmpty()
    {
        var indexPath = Path.Combine(_tempDir, "nonexistent.json");
        var loaded = ThumbnailIndex.Load(indexPath, _thumbBaseDir, new HashSet<string>());
        loaded.Should().BeEmpty();
    }

    [Fact]
    public void Save_EmptyList_WritesEmptyJson()
    {
        var indexPath = Path.Combine(_tempDir, "empty.json");
        ThumbnailIndex.Save(indexPath, new List<ThumbnailTask>());
        File.Exists(indexPath).Should().BeTrue();
        var loaded = ThumbnailIndex.Load(indexPath, _thumbBaseDir, new HashSet<string>());
        loaded.Should().BeEmpty();
    }

    [Fact]
    public void Load_ExcludesExistingPaths()
    {
        var indexPath = Path.Combine(_tempDir, "index.json");
        var tasks = new List<ThumbnailTask>
        {
            new() { VideoPath = "/videos/a.mp4", Md5Dir = "aaa" },
            new() { VideoPath = "/videos/b.mp4", Md5Dir = "bbb" }
        };
        ThumbnailIndex.Save(indexPath, tasks);

        var loaded = ThumbnailIndex.Load(indexPath, _thumbBaseDir,
            new HashSet<string> { "/videos/a.mp4" });

        loaded.Should().ContainSingle().Which.VideoPath.Should().Be("/videos/b.mp4");
    }

    [Fact]
    public void Load_DiskDirectoryExists_PromotesToReady()
    {
        var indexPath = Path.Combine(_tempDir, "index.json");
        var tasks = new List<ThumbnailTask>
        {
            new()
            {
                VideoPath = "/videos/hasdir.mp4",
                Md5Dir = "hash123",
                State = ThumbnailState.Generating,
                TotalFrames = 10
            }
        };
        ThumbnailIndex.Save(indexPath, tasks);

        var taskDir = Path.Combine(_thumbBaseDir, "hash123");
        Directory.CreateDirectory(taskDir);
        File.WriteAllText(Path.Combine(taskDir, "0001.jpg"), "");

        var loaded = ThumbnailIndex.Load(indexPath, _thumbBaseDir, new HashSet<string>());
        loaded.Should().ContainSingle().Which.State.Should().Be(ThumbnailState.Ready);
    }

    [Fact]
    public void Load_StateFailed_ResetsToPending()
    {
        var indexPath = Path.Combine(_tempDir, "index.json");
        var tasks = new List<ThumbnailTask>
        {
            new()
            {
                VideoPath = "/videos/failed.mp4",
                Md5Dir = "failhash",
                State = ThumbnailState.Failed,
                TotalFrames = 0
            }
        };
        ThumbnailIndex.Save(indexPath, tasks);

        var loaded = ThumbnailIndex.Load(indexPath, _thumbBaseDir, new HashSet<string>());
        loaded.Should().ContainSingle().Which.State.Should().Be(ThumbnailState.Pending);
    }
}



