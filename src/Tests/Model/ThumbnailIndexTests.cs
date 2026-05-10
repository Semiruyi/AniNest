using FluentAssertions;
using AniNest.Infrastructure.Logging;
using AniNest.Infrastructure.Paths;
using AniNest.Infrastructure.Persistence;
using AniNest.Infrastructure.Media;
using AniNest.Infrastructure.Thumbnails;
using Xunit;
using System.IO;

namespace AniNest.Tests.Model;

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
                TotalFrames = 60
            }
        };

        var taskDir = Path.Combine(_thumbBaseDir, "abc123");
        Directory.CreateDirectory(taskDir);
        var sourceDir = Path.Combine(_tempDir, "source-roundtrip");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllBytes(Path.Combine(sourceDir, "0001.jpg"), [1]);
        ThumbnailBundle.Write(sourceDir, taskDir, [0L]);

        ThumbnailIndex.Save(indexPath, tasks);
        File.Exists(indexPath).Should().BeTrue();

        var loaded = ThumbnailIndex.Load(indexPath, _thumbBaseDir, new HashSet<string>());
        loaded.Should().ContainSingle();
        loaded[0].VideoPath.Should().Be("/videos/test.mp4");
        loaded[0].Md5Dir.Should().Be("abc123");
        loaded[0].TotalFrames.Should().Be(60);
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
    public void PromoteIndexFile_ReplacesExistingFile()
    {
        var indexPath = Path.Combine(_tempDir, "index.json");
        var stagedPath = indexPath + ".tmp";
        File.WriteAllText(indexPath, "old");
        File.WriteAllText(stagedPath, "new");

        ThumbnailIndex.PromoteIndexFile(stagedPath, indexPath);

        File.Exists(stagedPath).Should().BeFalse();
        File.ReadAllText(indexPath).Should().Be("new");
        File.Exists(indexPath + ".bak").Should().BeFalse();
    }

    [Fact]
    public void PromoteIndexFile_WhenMoveFails_RestoresOriginalFile()
    {
        var indexPath = Path.Combine(_tempDir, "index.json");
        var stagedPath = indexPath + ".tmp";
        File.WriteAllText(indexPath, "old");
        File.WriteAllText(stagedPath, "new");

        int moveCount = 0;
        try
        {
            ThumbnailIndex.TestFileMoveOverride = (source, destination) =>
            {
                moveCount++;
                if (moveCount == 2)
                    throw new IOException("Injected move failure");

                File.Move(source, destination);
            };

            Action act = () => ThumbnailIndex.PromoteIndexFile(stagedPath, indexPath);

            act.Should().Throw<IOException>();
            File.ReadAllText(indexPath).Should().Be("old");
            File.Exists(stagedPath).Should().BeTrue();
        }
        finally
        {
            ThumbnailIndex.TestFileMoveOverride = null;
        }
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
        var sourceDir = Path.Combine(_tempDir, "source-promote");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllBytes(Path.Combine(sourceDir, "0001.jpg"), [1]);
        ThumbnailBundle.Write(sourceDir, taskDir, [0L]);

        var loaded = ThumbnailIndex.Load(indexPath, _thumbBaseDir, new HashSet<string>());
        loaded.Should().ContainSingle().Which.State.Should().Be(ThumbnailState.Ready);
    }

    [Fact]
    public void Load_BundleExists_PromotesToReady()
    {
        var indexPath = Path.Combine(_tempDir, "index.json");
        var tasks = new List<ThumbnailTask>
        {
            new()
            {
                VideoPath = "/videos/hasbundle.mp4",
                Md5Dir = "bundle123",
                State = ThumbnailState.Generating,
                TotalFrames = 10
            }
        };
        ThumbnailIndex.Save(indexPath, tasks);

        var taskDir = Path.Combine(_thumbBaseDir, "bundle123");
        Directory.CreateDirectory(taskDir);
        File.WriteAllBytes(ThumbnailBundle.GetBundlePath(taskDir), [1, 2, 3]);

        var loaded = ThumbnailIndex.Load(indexPath, _thumbBaseDir, new HashSet<string>());
        loaded.Should().ContainSingle().Which.State.Should().Be(ThumbnailState.Ready);
        loaded.Should().ContainSingle().Which.TotalFrames.Should().Be(10);
    }

    [Fact]
    public void Load_BundleExistsWithZeroIndexedFrames_UsesBundleFrameCount()
    {
        var indexPath = Path.Combine(_tempDir, "index.json");
        var tasks = new List<ThumbnailTask>
        {
            new()
            {
                VideoPath = "/videos/bundlezero.mp4",
                Md5Dir = "bundlezero",
                State = ThumbnailState.Ready,
                TotalFrames = 0
            }
        };
        ThumbnailIndex.Save(indexPath, tasks);

        var sourceDir = Path.Combine(_tempDir, "bundle-source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllBytes(Path.Combine(sourceDir, "0001.jpg"), [1]);
        File.WriteAllBytes(Path.Combine(sourceDir, "0002.jpg"), [2]);

        var taskDir = Path.Combine(_thumbBaseDir, "bundlezero");
        Directory.CreateDirectory(taskDir);
        ThumbnailBundle.Write(sourceDir, taskDir, [0L, 1000L]);

        var loaded = ThumbnailIndex.Load(indexPath, _thumbBaseDir, new HashSet<string>());
        loaded.Should().ContainSingle().Which.TotalFrames.Should().Be(2);
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

    [Fact]
    public void Load_StatePausedGenerating_ResetsToPending()
    {
        var indexPath = Path.Combine(_tempDir, "index.json");
        var tasks = new List<ThumbnailTask>
        {
            new()
            {
                VideoPath = "/videos/paused.mp4",
                Md5Dir = "pausedhash",
                State = ThumbnailState.PausedGenerating,
                TotalFrames = 0
            }
        };
        ThumbnailIndex.Save(indexPath, tasks);

        var loaded = ThumbnailIndex.Load(indexPath, _thumbBaseDir, new HashSet<string>());
        loaded.Should().ContainSingle().Which.State.Should().Be(ThumbnailState.Pending);
    }
}



