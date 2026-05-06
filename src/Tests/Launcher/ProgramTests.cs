using LocalPlayer.Launcher;

namespace LocalPlayer.Tests.Launcher;

public class ProgramTests
{
    [Fact]
    public void ResolveAppDirectory_ReturnsAppDirectoryUnderRoot()
    {
        var root = CreateTempDirectory();
        var appDir = Path.Combine(root, "app");
        Directory.CreateDirectory(appDir);

        try
        {
            var resolved = LauncherPaths.ResolveAppDirectory(root);

            resolved.Should().Be(appDir);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"LocalPlayer.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
