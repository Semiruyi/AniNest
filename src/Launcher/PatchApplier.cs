using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace AniNest.Launcher;

public sealed class PatchApplier
{
    private readonly string _rootDirectory;
    private readonly string _appDirectory;
    private readonly string _backupDirectory;

    public PatchApplier(string rootDirectory)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory);
        _appDirectory = Path.Combine(_rootDirectory, "app");
        _backupDirectory = Path.Combine(_rootDirectory, "backup", "last-good");
    }

    public string AppDirectory => _appDirectory;

    public UpdateResult ApplyPackage(string packagePath)
    {
        if (!File.Exists(packagePath))
            return UpdateResult.Fail($"Package not found: {packagePath}");

        try
        {
            using var archive = ZipFile.OpenRead(packagePath);
            var manifest = ReadManifest(archive);
            if (manifest == null)
                return UpdateResult.Fail("manifest.json not found in package");

            var currentVersion = GetCurrentVersion();
            if (!string.IsNullOrWhiteSpace(manifest.BaseVersion) &&
                !string.Equals(manifest.BaseVersion, currentVersion, StringComparison.OrdinalIgnoreCase))
            {
                return UpdateResult.Fail($"Base version mismatch. Current={currentVersion}, Required={manifest.BaseVersion}");
            }

            EnsureAppRoot();
            PrepareBackup();

            try
            {
                ApplyEntries(archive, manifest);
                WriteInstalledManifest(manifest);
            }
            catch
            {
                RestoreBackup();
                throw;
            }

            return UpdateResult.Ok(manifest.Version, currentVersion);
        }
        catch (Exception ex)
        {
            return UpdateResult.Fail(ex.Message);
        }
    }

    public string GetCurrentVersion()
    {
        var manifestPath = Path.Combine(_appDirectory, "manifest.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize(json, LauncherJsonContext.Default.PatchManifest);
                if (manifest != null && !string.IsNullOrWhiteSpace(manifest.Version))
                    return manifest.Version;
            }
            catch
            {
            }
        }

        var exePath = Path.Combine(_appDirectory, "AniNest.exe");
        if (File.Exists(exePath))
        {
            try
            {
                var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(info.ProductVersion))
                    return info.ProductVersion!;
            }
            catch
            {
            }
        }

        return "0.0.0";
    }

    private static PatchManifest? ReadManifest(ZipArchive archive)
    {
        var entry = archive.GetEntry("manifest.json");
        if (entry == null)
            return null;

        using var stream = entry.Open();
        return JsonSerializer.Deserialize(stream, LauncherJsonContext.Default.PatchManifest);
    }

    private void EnsureAppRoot()
    {
        Directory.CreateDirectory(_appDirectory);
        Directory.CreateDirectory(Path.Combine(_rootDirectory, "backup"));
    }

    private void PrepareBackup()
    {
        if (Directory.Exists(_backupDirectory))
            Directory.Delete(_backupDirectory, true);

        CopyDirectory(_appDirectory, _backupDirectory);
    }

    private void RestoreBackup()
    {
        if (!Directory.Exists(_backupDirectory))
            return;

        if (Directory.Exists(_appDirectory))
            Directory.Delete(_appDirectory, true);

        CopyDirectory(_backupDirectory, _appDirectory);
    }

    private void ApplyEntries(ZipArchive archive, PatchManifest manifest)
    {
        foreach (var file in manifest.Files)
        {
            var relativePath = NormalizeRelativePath(file.Path);
            var targetPath = ResolveTargetPath(relativePath);
            var action = file.Action.Trim().ToLowerInvariant();

            if (action == "delete")
            {
                DeleteFileWithRetry(targetPath);
                continue;
            }

            var entry = archive.GetEntry(relativePath.Replace(Path.DirectorySeparatorChar, '/'));
            if (entry == null)
                throw new InvalidOperationException($"Missing payload for {relativePath}");

            ApplySingleFile(entry, targetPath, file.Sha256, relativePath);
        }
    }

    private static void ApplySingleFile(ZipArchiveEntry entry, string targetPath, string? expectedSha256, string relativePath)
    {
        const int attempts = 5;
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                var targetDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(targetDir))
                    Directory.CreateDirectory(targetDir);

                var tempPath = targetPath + ".tmp";
                using (var source = entry.Open())
                using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    source.CopyTo(output);
                }

                if (!string.IsNullOrWhiteSpace(expectedSha256))
                {
                    var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(tempPath))).ToLowerInvariant();
                    if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"Hash mismatch for {relativePath}");
                }

                File.Copy(tempPath, targetPath, true);
                File.Delete(tempPath);
                return;
            }
            catch when (i < attempts - 1)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static void DeleteFileWithRetry(string targetPath)
    {
        const int attempts = 5;
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                if (File.Exists(targetPath))
                    File.Delete(targetPath);
                return;
            }
            catch when (i < attempts - 1)
            {
                Thread.Sleep(100);
            }
        }
    }

    private void WriteInstalledManifest(PatchManifest manifest)
    {
        var manifestPath = Path.Combine(_appDirectory, "manifest.json");
        var json = JsonSerializer.Serialize(manifest, LauncherJsonContext.Default.PatchManifest);
        File.WriteAllText(manifestPath, json);
    }

    private static string NormalizeRelativePath(string path)
    {
        path = path.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
        if (path.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException($"Invalid path: {path}");
        return path;
    }

    private string ResolveTargetPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_appDirectory, relativePath));
        var root = Path.GetFullPath(_appDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path escapes app root: {relativePath}");
        return fullPath;
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(sourceDir))
            return;

        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destination = Path.Combine(targetDir, relative);
            var destinationDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(destinationDir))
                Directory.CreateDirectory(destinationDir);
            CopyFileWithRetry(file, destination);
        }
    }

    private static void CopyFileWithRetry(string sourcePath, string destinationPath)
    {
        const int attempts = 5;
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                source.CopyTo(target);
                return;
            }
            catch when (i < attempts - 1)
            {
                Thread.Sleep(100);
            }
        }
    }
}

public sealed record UpdateResult(bool Success, string Message, string? FromVersion = null, string? ToVersion = null)
{
    public static UpdateResult Ok(string toVersion, string fromVersion)
        => new(true, "OK", fromVersion, toVersion);

    public static UpdateResult Fail(string message)
        => new(false, message);
}
