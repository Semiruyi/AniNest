using System.IO;
using FluentAssertions;
using AniNest.Infrastructure.Thumbnails;

namespace AniNest.Tests.Model;

public class ThumbnailBundleTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sourceDir;
    private readonly string _targetDir;

    public ThumbnailBundleTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ThumbnailBundleTests_{Guid.NewGuid():N}");
        _sourceDir = Path.Combine(_tempDir, "source");
        _targetDir = Path.Combine(_tempDir, "target");
        Directory.CreateDirectory(_sourceDir);
        Directory.CreateDirectory(_targetDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Write_ReadFrameBytes_RoundTripsFrames()
    {
        File.WriteAllBytes(Path.Combine(_sourceDir, "0001.jpg"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(_sourceDir, "0002.jpg"), [4, 5]);

        ThumbnailBundle.Write(_sourceDir, _targetDir, [0L, 500L]);

        ThumbnailBundle.Exists(_targetDir).Should().BeTrue();
        ThumbnailBundle.ReadFrameBytes(_targetDir, 0).Should().Equal([1, 2, 3]);
        ThumbnailBundle.ReadFrameBytes(_targetDir, 1).Should().Equal([4, 5]);
        ThumbnailBundle.ReadFramePositions(_targetDir).Should().Equal([0L, 500L]);
    }
}
