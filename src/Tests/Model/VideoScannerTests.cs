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
    private readonly IVideoScanner _scanner = new VideoScanner();
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
        var result = _scanner.ScanFolder(Path.Combine(_tempDir, "nope"));
        result.VideoCount.Should().Be(0);
        result.CoverPath.Should().BeNull();
    }

    [Fact]
    public void ScanFolder_VideoFiles_CountsThem()
    {
        CreateFile("movie.mp4");
        CreateFile("show.mkv");
        CreateFile("notes.txt");

        var result = _scanner.ScanFolder(_tempDir);

        result.VideoCount.Should().Be(2);
    }

    [Fact]
    public void ScanFolder_AllExtensions_Recognized()
    {
        var exts = new[] { "mp4", "mkv", "avi", "mov", "wmv", "flv", "webm", "m4v", "mpg", "mpeg", "ts", "m2ts", "rmvb" };
        foreach (var ext in exts)
            CreateFile($"video.{ext}");

        var result = _scanner.ScanFolder(_tempDir);

        result.VideoCount.Should().Be(13);
    }

    [Fact]
    public void ScanFolder_CoverJpg_Found()
    {
        CreateFile("video.mp4");
        CreateFile("folder.jpg");

        var result = _scanner.ScanFolder(_tempDir);

        result.CoverPath.Should().EndWith("folder.jpg");
    }

    [Fact]
    public void ScanFolder_CoverPng_Found()
    {
        CreateFile("video.mp4");
        CreateFile("cover.png");

        var result = _scanner.ScanFolder(_tempDir);

        result.CoverPath.Should().EndWith("cover.png");
    }

    [Fact]
    public void ScanFolder_PreferredNamesFirst()
    {
        CreateFile("video.mp4");
        CreateFile("thumb.jpg");
        CreateFile("folder.jpg");

        var result = _scanner.ScanFolder(_tempDir);

        result.CoverPath.Should().EndWith("folder.jpg");
    }

    [Fact]
    public void ScanFolder_NoCover_ReturnsNull()
    {
        CreateFile("video.mp4");

        var result = _scanner.ScanFolder(_tempDir);

        result.CoverPath.Should().BeNull();
    }

    [Fact]
    public void ScanFolder_AnyImageAsFallback()
    {
        CreateFile("video.mp4");
        CreateFile("screenshot.bmp");

        var result = _scanner.ScanFolder(_tempDir);

        result.CoverPath.Should().EndWith("screenshot.bmp");
    }

    [Fact]
    public void GetVideoFiles_SortsByName()
    {
        CreateFile("c.mp4");
        CreateFile("a.mp4");
        CreateFile("b.mp4");

        var files = _scanner.GetVideoFiles(_tempDir);

        files.Should().BeInAscendingOrder();
    }

    [Fact]
    public void GetVideoFiles_EmptyDir_ReturnsEmpty()
    {
        var files = _scanner.GetVideoFiles(_tempDir);
        files.Should().BeEmpty();
    }

    [Fact]
    public void GetVideoFiles_IgnoresNonVideo()
    {
        CreateFile("readme.txt");
        CreateFile("video.mp4");

        var files = _scanner.GetVideoFiles(_tempDir);

        files.Should().ContainSingle().Which.Should().EndWith("video.mp4");
    }

    [Fact]
    public void CountVideosInFolder_Matches()
    {
        CreateFile("a.mp4");
        CreateFile("b.mkv");
        CreateFile("c.txt");

        var count = _scanner.CountVideosInFolder(_tempDir);

        count.Should().Be(2);
    }

    [Fact]
    public void FindCoverImage_PrefersNamed()
    {
        CreateFile("random.png");
        CreateFile("poster.jpg");

        var cover = _scanner.FindCoverImage(_tempDir);

        cover.Should().EndWith("poster.jpg");
    }

    // ── FindVideoFolders ──────────────────────────────────────────

    [Fact]
    public void FindVideoFolders_RootHasVideos_AddsRoot()
    {
        CreateFile("movie.mp4");

        var result = _scanner.FindVideoFolders(_tempDir);

        result.Should().ContainSingle().Which.Should().Be(_tempDir);
    }

    [Fact]
    public void FindVideoFolders_OneLevel_AllAdded()
    {
        var subA = CreateSubDir("ShowA");
        var subB = CreateSubDir("ShowB");
        CreateFileIn(subA, "01.mp4");
        CreateFileIn(subB, "01.mkv");

        var result = _scanner.FindVideoFolders(_tempDir);

        result.Should().BeEquivalentTo(new[] { subA, subB });
    }

    [Fact]
    public void FindVideoFolders_MultiLevel_DeepestWins()
    {
        var level1 = CreateSubDir("Level1");
        var level2 = CreateSubDir("Level2", level1);
        var level3 = CreateSubDir("Level3", level2);
        CreateFileIn(level3, "video.mp4");

        var result = _scanner.FindVideoFolders(_tempDir);

        result.Should().ContainSingle().Which.Should().Be(level3);
    }

    [Fact]
    public void FindVideoFolders_Mixed_DifferentDepths()
    {
        var hasVideo = CreateSubDir("HasVideo");
        CreateFileIn(hasVideo, "01.mp4");

        var noVideo = CreateSubDir("NoVideo");
        var deep = CreateSubDir("Deep", noVideo);
        CreateFileIn(deep, "01.mkv");

        var result = _scanner.FindVideoFolders(_tempDir);

        result.Should().BeEquivalentTo(new[] { hasVideo, deep });
    }

    [Fact]
    public void FindVideoFolders_EmptyDir_ReturnsEmpty()
    {
        var result = _scanner.FindVideoFolders(_tempDir);

        result.Should().BeEmpty();
    }

    [Fact]
    public void FindVideoFolders_NonExistent_ReturnsEmpty()
    {
        var result = _scanner.FindVideoFolders(Path.Combine(_tempDir, "nope"));

        result.Should().BeEmpty();
    }

    [Fact]
    public void FindVideoFolders_SkipsNonVideoSubfolders()
    {
        var onlyImages = CreateSubDir("Images");
        CreateFileIn(onlyImages, "photo.jpg");
        var hasVideo = CreateSubDir("Videos");
        CreateFileIn(hasVideo, "movie.mp4");

        var result = _scanner.FindVideoFolders(_tempDir);

        result.Should().ContainSingle().Which.Should().Be(hasVideo);
    }

    [Fact]
    public void FindVideoFolders_RootAndSubfoldersBothHaveVideos_AllAdded()
    {
        CreateFile("root-video.mp4");
        var sub = CreateSubDir("Sub");
        CreateFileIn(sub, "sub-video.mkv");

        var result = _scanner.FindVideoFolders(_tempDir);

        result.Should().BeEquivalentTo(new[] { _tempDir, sub });
    }

    private void CreateFile(string name)
    {
        File.WriteAllText(Path.Combine(_tempDir, name), "");
    }

    private static void CreateFileIn(string dir, string name)
    {
        File.WriteAllText(Path.Combine(dir, name), "");
    }

    private string CreateSubDir(string name, string? parent = null)
    {
        var path = Path.Combine(parent ?? _tempDir, name);
        Directory.CreateDirectory(path);
        return path;
    }
}



