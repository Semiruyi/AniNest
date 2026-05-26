using AniNest.Host.Modules;

namespace AniNest.Backend.Tests;

public sealed class ServerDirectoryBrowserTests
{
    [Fact]
    public async Task BrowseAsync_ReturnsDirectoriesWithinAllowedRoot()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        var seasonA = Path.Combine(testRoot, "Season A");
        var seasonB = Path.Combine(testRoot, "Season B");
        Directory.CreateDirectory(seasonA);
        Directory.CreateDirectory(seasonB);

        var browser = new ServerDirectoryBrowser(testRoot);

        var payload = await browser.BrowseAsync(null);

        Assert.Equal(Path.GetFullPath(testRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), payload.RootPath);
        Assert.Equal(payload.RootPath, payload.CurrentPath);
        Assert.Null(payload.ParentPath);
        Assert.True(payload.CanSelect);
        Assert.Equal(2, payload.Directories.Count);
        Assert.Contains(payload.Directories, item => item.Path == Path.GetFullPath(seasonA));
        Assert.Contains(payload.Directories, item => item.Path == Path.GetFullPath(seasonB));
    }

    [Fact]
    public async Task BrowseAsync_RejectsPathOutsideAllowedRoot()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "AniNest.Backend.Tests", $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        var outsidePath = Path.GetTempPath();

        var browser = new ServerDirectoryBrowser(testRoot);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => browser.BrowseAsync(outsidePath));

        Assert.Contains("outside the allowed root", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
