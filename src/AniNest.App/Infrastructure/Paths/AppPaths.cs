using System.IO;

namespace AniNest.Infrastructure.Paths;

public static class AppPaths
{
    public static string AppRootDirectory { get; } = AppContext.BaseDirectory;
    public static string ResourceDataDirectory { get; } = Path.Combine(AppRootDirectory, "Data");
    public static string LanguagesDirectory { get; } = Path.Combine(ResourceDataDirectory, "Languages");

    public static string UserDataDirectory { get; } = Path.Combine(AppRootDirectory, "data");
    public static string ConfigDirectory { get; } = Path.Combine(UserDataDirectory, "config");
    public static string LogsDirectory { get; } = Path.Combine(UserDataDirectory, "logs");
    public static string CacheDirectory { get; } = Path.Combine(UserDataDirectory, "cache");
    public static string ThumbnailDirectory { get; } = Path.Combine(CacheDirectory, "thumbnails");
    public static string MetadataDirectory { get; } = Path.Combine(CacheDirectory, "metadata");
    public static string MetadataPosterDirectory { get; } = Path.Combine(MetadataDirectory, "posters");
    public static string UpdateDirectory { get; } = Path.Combine(UserDataDirectory, "updates");
    public static string BackupDirectory { get; } = Path.Combine(UserDataDirectory, "backup");

    public static string SettingsPath { get; } = Path.Combine(ConfigDirectory, "settings.json");
    public static string PlayerLogPath { get; } = Path.Combine(LogsDirectory, "player.log");
    public static string PerfLogPath { get; } = Path.Combine(LogsDirectory, "perf.log");
    public static string FfmpegPath { get; } = ResolveFfmpegPath();

    static AppPaths()
    {
        Directory.CreateDirectory(ResourceDataDirectory);
        Directory.CreateDirectory(LanguagesDirectory);
        Directory.CreateDirectory(UserDataDirectory);
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(ThumbnailDirectory);
        Directory.CreateDirectory(MetadataDirectory);
        Directory.CreateDirectory(MetadataPosterDirectory);
        Directory.CreateDirectory(UpdateDirectory);
        Directory.CreateDirectory(BackupDirectory);
    }

    public static string ResolveInLogs(string fileName)
        => Path.IsPathRooted(fileName) ? fileName : Path.Combine(LogsDirectory, fileName);

    private static string ResolveFfmpegPath()
    {
        string appRootPath = Path.Combine(AppRootDirectory, "ffmpeg.exe");
        if (File.Exists(appRootPath))
            return appRootPath;

        string repoToolsPath = Path.GetFullPath(Path.Combine(AppRootDirectory, "..", "..", "..", "..", "tools", "ffmpeg", "ffmpeg.exe"));
        if (File.Exists(repoToolsPath))
            return repoToolsPath;

        return appRootPath;
    }
}



