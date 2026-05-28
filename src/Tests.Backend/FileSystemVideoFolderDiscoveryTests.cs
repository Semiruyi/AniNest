using AniNest.Host.Modules;

namespace AniNest.Backend.Tests;

public sealed class FileSystemVideoFolderDiscoveryTests
{
    [Fact]
    public void FindDescendantVideoFolders_ReturnsNestedFoldersWithSupportedVideos()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);

        var directMatch = Path.Combine(testRoot, "Season A");
        var nestedParent = Path.Combine(testRoot, "Series");
        var nestedMatch = Path.Combine(nestedParent, "Season B");
        var invalid = Path.Combine(testRoot, "Notes");

        Directory.CreateDirectory(directMatch);
        Directory.CreateDirectory(nestedMatch);
        Directory.CreateDirectory(invalid);

        File.WriteAllText(Path.Combine(testRoot, "root-video.mp4"), string.Empty);
        File.WriteAllText(Path.Combine(directMatch, "Episode 01.mkv"), string.Empty);
        File.WriteAllText(Path.Combine(nestedMatch, "Episode 01.mp4"), string.Empty);
        File.WriteAllText(Path.Combine(invalid, "readme.txt"), string.Empty);

        var discovery = new FileSystemVideoFolderDiscovery();

        var result = discovery.FindDescendantVideoFolders(testRoot);

        Assert.Equal(2, result.Count);
        Assert.Equal(
            [
                directMatch,
                nestedMatch
            ],
            result);
    }
}
