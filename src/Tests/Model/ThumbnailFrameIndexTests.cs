using System;
using System.IO;
using FluentAssertions;
using AniNest.Infrastructure.Thumbnails;
using Xunit;

namespace AniNest.Tests.Model;

public class ThumbnailFrameIndexTests : IDisposable
{
    private readonly string _tempDir;

    public ThumbnailFrameIndexTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ThumbnailFrameIndexTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void ResolveThumbnailPath_UsesLegacySequentialNaming_WhenNoFrameIndexExists()
    {
        string expectedPath = Path.Combine(_tempDir, "0003.jpg");
        File.WriteAllText(expectedPath, string.Empty);

        string? resolved = ThumbnailFrameIndex.ResolveThumbnailPath(_tempDir, 2);

        resolved.Should().Be(expectedPath);
    }

    [Fact]
    public void ResolveThumbnailPath_UsesNearestFrameFromIndex()
    {
        ThumbnailFrameIndex.Save(_tempDir, [0L, 5000L, 12000L]);
        File.WriteAllText(Path.Combine(_tempDir, "0001.jpg"), string.Empty);
        File.WriteAllText(Path.Combine(_tempDir, "0002.jpg"), string.Empty);
        File.WriteAllText(Path.Combine(_tempDir, "0003.jpg"), string.Empty);

        string? resolved = ThumbnailFrameIndex.ResolveThumbnailPath(_tempDir, 2000L);

        resolved.Should().Be(Path.Combine(_tempDir, "0001.jpg"));
    }

    [Fact]
    public void FindNearestFrameIndex_PrefersPreviousFrameOnTie()
    {
        int index = ThumbnailFrameIndex.FindNearestFrameIndex([0L, 10000L], 5000L);

        index.Should().Be(0);
    }

    [Fact]
    public void ResolveThumbnailPath_UsesMillisecondPrecisionFromIndex()
    {
        ThumbnailFrameIndex.Save(_tempDir, [0L, 500L, 1000L]);
        File.WriteAllText(Path.Combine(_tempDir, "0001.jpg"), string.Empty);
        File.WriteAllText(Path.Combine(_tempDir, "0002.jpg"), string.Empty);
        File.WriteAllText(Path.Combine(_tempDir, "0003.jpg"), string.Empty);

        string? resolved = ThumbnailFrameIndex.ResolveThumbnailPath(_tempDir, 600L);

        resolved.Should().Be(Path.Combine(_tempDir, "0002.jpg"));
    }

    [Fact]
    public void Load_UsesBundleFramePositions_WhenFramesJsonDoesNotExist()
    {
        string sourceDir = Path.Combine(_tempDir, "source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllBytes(Path.Combine(sourceDir, "0001.jpg"), [1]);
        File.WriteAllBytes(Path.Combine(sourceDir, "0002.jpg"), [2]);

        ThumbnailBundle.Write(sourceDir, _tempDir, [0L, 750L]);

        long[]? loaded = ThumbnailFrameIndex.Load(_tempDir);

        loaded.Should().Equal([0L, 750L]);
    }
}
