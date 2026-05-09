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

    [Fact]
    public void BundleWriter_AppendAndCommit_RoundTripsFrames()
    {
        using (var writer = ThumbnailBundle.CreateWriter(_targetDir))
        {
            writer.AppendFrame(0L, [1, 2, 3]);
            writer.AppendFrame(1250L, [4, 5, 6, 7]);
            writer.Commit();
        }

        ThumbnailBundle.Exists(_targetDir).Should().BeTrue();
        ThumbnailBundle.ReadFrameBytes(_targetDir, 0).Should().Equal([1, 2, 3]);
        ThumbnailBundle.ReadFrameBytes(_targetDir, 1).Should().Equal([4, 5, 6, 7]);
        ThumbnailBundle.ReadFramePositions(_targetDir).Should().Equal([0L, 1250L]);
        ThumbnailBundle.GetFrameCount(_targetDir).Should().Be(2);
    }

    [Fact]
    public void BundleWriter_DisposeWithoutCommit_DeletesTemporaryFile()
    {
        string tempBundlePath = ThumbnailBundle.GetBundlePath(_targetDir) + ".tmp";
        string tempPayloadPath = tempBundlePath + ".payload";

        using (var writer = ThumbnailBundle.CreateWriter(_targetDir))
        {
            writer.AppendFrame(0L, [9, 8, 7]);
            File.Exists(tempPayloadPath).Should().BeTrue();
        }

        File.Exists(tempBundlePath).Should().BeFalse();
        File.Exists(tempPayloadPath).Should().BeFalse();
        ThumbnailBundle.Exists(_targetDir).Should().BeFalse();
    }

    [Fact]
    public void BundleWriter_UpdateFramePosition_PersistsUpdatedPositions()
    {
        using (var writer = ThumbnailBundle.CreateWriter(_targetDir))
        {
            writer.AppendFrame(0L, [1, 2]);
            writer.AppendFrame(1000L, [3, 4]);
            writer.UpdateFramePosition(1, 2500L);
            writer.Commit();
        }

        ThumbnailBundle.ReadFramePositions(_targetDir).Should().Equal([0L, 2500L]);
    }

    [Fact]
    public void PromoteBundleFile_ReplacesExistingFile()
    {
        string stagedPath = Path.Combine(_targetDir, "bundle.bin.tmp");
        string finalPath = ThumbnailBundle.GetBundlePath(_targetDir);
        File.WriteAllBytes(stagedPath, [9, 8, 7]);
        File.WriteAllBytes(finalPath, [1, 2, 3]);

        ThumbnailBundle.PromoteBundleFile(stagedPath, finalPath);

        File.Exists(stagedPath).Should().BeFalse();
        File.ReadAllBytes(finalPath).Should().Equal([9, 8, 7]);
        File.Exists(finalPath + ".bak").Should().BeFalse();
    }

    [Fact]
    public void PromoteBundleFile_WhenMoveFails_RestoresOriginalFile()
    {
        string stagedPath = Path.Combine(_targetDir, "bundle.bin.tmp");
        string finalPath = ThumbnailBundle.GetBundlePath(_targetDir);
        File.WriteAllBytes(stagedPath, [9, 8, 7]);
        File.WriteAllBytes(finalPath, [1, 2, 3]);

        int moveCount = 0;
        try
        {
            ThumbnailBundle.TestFileMoveOverride = (source, destination) =>
            {
                moveCount++;
                if (moveCount == 2)
                    throw new IOException("Injected move failure");

                File.Move(source, destination);
            };

            Action act = () => ThumbnailBundle.PromoteBundleFile(stagedPath, finalPath);

            act.Should().Throw<IOException>();
            File.ReadAllBytes(finalPath).Should().Equal([1, 2, 3]);
            File.Exists(stagedPath).Should().BeTrue();
        }
        finally
        {
            ThumbnailBundle.TestFileMoveOverride = null;
        }
    }
}
