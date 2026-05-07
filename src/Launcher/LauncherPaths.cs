namespace AniNest.Launcher;

public static class LauncherPaths
{
    public static string ResolveAppDirectory(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return Path.Combine(root, "app");
    }
}
