using System.IO.Compression;
using FluentAssertions;
using AniNest.Launcher;
using Xunit;

namespace AniNest.Tests.Launcher;

public class LauncherPackageLocatorTests : IDisposable
{
    private readonly string _root;

    public LauncherPackageLocatorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"AniNestLauncherLocatorTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void FindPendingPackage_FindsZipBesideLauncher()
    {
        var zipPath = Path.Combine(_root, "update.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("manifest.json");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("{}");
        }

        var result = LauncherPackageLocator.FindPendingPackage(_root);

        result.Should().Be(zipPath);
    }

    [Fact]
    public void FindPendingPackage_IgnoresZipWithoutManifest()
    {
        var zipPath = Path.Combine(_root, "not-update.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("file.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("x");
        }

        var result = LauncherPackageLocator.FindPendingPackage(_root);

        result.Should().BeNull();
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }
}
