using FluentAssertions;
using LocalPlayer.Infrastructure.Logging;
using LocalPlayer.Infrastructure.Paths;
using LocalPlayer.Infrastructure.Persistence;
using LocalPlayer.Infrastructure.Media;
using LocalPlayer.Infrastructure.Thumbnails;
using Xunit;
using System.IO;

namespace LocalPlayer.Tests.Model;

public class VideoScannerTests : IDisposable
{
    private readonly string _tempDir;

    public VideoScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"VideoScannerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void ScanFolder_NonExistent_ReturnsZero()
    {
        var result = VideoScanner.ScanFolder(Path.Combine(_tempDir, "nope"));
        result.VideoCount.Should().Be(0);
        result.CoverPath.Should().BeNull();
    }

    [Fact]
    public void ScanFolder_VideoFiles_CountsThem()
    {
        CreateFile("movie.mp4");
        CreateFile("show.mkv");
        CreateFile("notes.txt");

        var result = VideoScanner.ScanFolder(_tempDir);

        result.VideoCount.Should().Be(2);
    }

    [Fact]
    public void ScanFolder_AllExtensions_Recognized()
    {
        var exts = new[] { "mp4", "mkv", "avi", "mov", "wmv", "flv", "webm", "m4v", "mpg", "mpeg", "ts", "m2ts", "rmvb" };
        foreach (var ext in exts)
            CreateFile($"video.{ext}");

        var result = VideoScanner.ScanFolder(_tempDir);

        result.VideoCount.Should().Be(13);
    }

    [Fact]
    public void ScanFolder_CoverJpg_Found()
    {
        CreateFile("video.mp4");
        CreateFile("folder.jpg");

        var result = VideoScanner.ScanFolder(_tempDir);

        result.CoverPath.Should().EndWith("folder.jpg");
    }

    [Fact]
    public void ScanFolder_CoverPng_Found()
    {
        CreateFile("video.mp4");
        CreateFile("cover.png");

        var result = VideoScanner.ScanFolder(_tempDir);

        result.CoverPath.Should().EndWith("cover.png");
    }

    [Fact]
    public void ScanFolder_PreferredNamesFirst()
    {
        CreateFile("video.mp4");
        CreateFile("thumb.jpg");
        CreateFile("folder.jpg");

        var result = VideoScanner.ScanFolder(_tempDir);

        result.CoverPath.Should().EndWith("folder.jpg");
    }

    [Fact]
    public void ScanFolder_NoCover_ReturnsNull()
    {
        CreateFile("video.mp4");

        var result = VideoScanner.ScanFolder(_tempDir);

        result.CoverPath.Should().BeNull();
    }

    [Fact]
    public void ScanFolder_AnyImageAsFallback()
    {
        CreateFile("video.mp4");
        CreateFile("screenshot.bmp");

        var result = VideoScanner.ScanFolder(_tempDir);

        result.CoverPath.Should().EndWith("screenshot.bmp");
    }

    [Fact]
    public void GetVideoFiles_SortsByName()
    {
        CreateFile("c.mp4");
        CreateFile("a.mp4");
        CreateFile("b.mp4");

        var files = VideoScanner.GetVideoFiles(_tempDir);

        files.Should().BeInAscendingOrder();
    }

    [Fact]
    public void GetVideoFiles_EmptyDir_ReturnsEmpty()
    {
        var files = VideoScanner.GetVideoFiles(_tempDir);
        files.Should().BeEmpty();
    }

    [Fact]
    public void GetVideoFiles_IgnoresNonVideo()
    {
        CreateFile("readme.txt");
        CreateFile("video.mp4");

        var files = VideoScanner.GetVideoFiles(_tempDir);

        files.Should().ContainSingle().Which.Should().EndWith("video.mp4");
    }

    [Fact]
    public void CountVideosInFolder_Matches()
    {
        CreateFile("a.mp4");
        CreateFile("b.mkv");
        CreateFile("c.txt");

        var count = VideoScanner.CountVideosInFolder(_tempDir);

        count.Should().Be(2);
    }

    [Fact]
    public void FindCoverImage_PrefersNamed()
    {
        CreateFile("random.png");
        CreateFile("poster.jpg");

        var cover = VideoScanner.FindCoverImage(_tempDir);

        cover.Should().EndWith("poster.jpg");
    }

    private void CreateFile(string name)
    {
        File.WriteAllText(Path.Combine(_tempDir, name), "");
    }
}



