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
        ThumbnailFrameIndex.Save(_tempDir, new[] { 0, 5, 12 });
        File.WriteAllText(Path.Combine(_tempDir, "0001.jpg"), string.Empty);
        File.WriteAllText(Path.Combine(_tempDir, "0002.jpg"), string.Empty);
        File.WriteAllText(Path.Combine(_tempDir, "0003.jpg"), string.Empty);

        string? resolved = ThumbnailFrameIndex.ResolveThumbnailPath(_tempDir, 7);

        resolved.Should().Be(Path.Combine(_tempDir, "0002.jpg"));
    }

    [Fact]
    public void FindNearestFrameIndex_PrefersPreviousFrameOnTie()
    {
        int index = ThumbnailFrameIndex.FindNearestFrameIndex(new[] { 0, 10 }, 5);

        index.Should().Be(0);
    }
}
