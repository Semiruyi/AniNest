using System.IO;
using FluentAssertions;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Tests.Model;

public class ThumbnailFrameIndexTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sourceDir;

    public ThumbnailFrameIndexTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ThumbnailFrameIndexTests_{Guid.NewGuid():N}");
        _sourceDir = Path.Combine(_tempDir, "source");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_sourceDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Load_UsesBundleFramePositions()
    {
        File.WriteAllBytes(Path.Combine(_sourceDir, "0001.jpg"), [1]);
        File.WriteAllBytes(Path.Combine(_sourceDir, "0002.jpg"), [2]);
        ThumbnailBundle.Write(_sourceDir, _tempDir, [0L, 750L]);

        long[]? loaded = ThumbnailFrameIndex.Load(_tempDir);

        loaded.Should().Equal([0L, 750L]);
    }

    [Fact]
    public void ResolveFrameIndex_UsesNearestFrameFromBundleMetadata()
    {
        File.WriteAllBytes(Path.Combine(_sourceDir, "0001.jpg"), [1]);
        File.WriteAllBytes(Path.Combine(_sourceDir, "0002.jpg"), [2]);
        File.WriteAllBytes(Path.Combine(_sourceDir, "0003.jpg"), [3]);
        ThumbnailBundle.Write(_sourceDir, _tempDir, [0L, 500L, 1000L]);

        int? resolved = ThumbnailFrameIndex.ResolveFrameIndex(_tempDir, 600L);

        resolved.Should().Be(1);
    }

    [Fact]
    public void FindNearestFrameIndex_PrefersPreviousFrameOnTie()
    {
        int index = ThumbnailFrameIndex.FindNearestFrameIndex([0L, 10000L], 5000L);

        index.Should().Be(0);
    }
}
