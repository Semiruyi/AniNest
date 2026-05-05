using System.IO.Compression;

namespace LocalPlayer.Launcher;

internal static class Program
{
    private static int Main(string[] args)
    {
        var root = AppContext.BaseDirectory;
        var applier = new PatchApplier(root);

        if (TryGetApplyPath(args, out var packagePath))
        {
            return ApplyPackageAndReport(applier, packagePath);
        }

        var pendingPackage = FindPendingPackage(root);
        if (!string.IsNullOrWhiteSpace(pendingPackage))
        {
            var code = ApplyPackageAndReport(applier, pendingPackage);
            if (code != 0)
                return code;
        }

        return LaunchApp(applier.AppDirectory);
    }

    private static int ApplyPackageAndReport(PatchApplier applier, string packagePath)
    {
        Console.WriteLine($"Applying patch: {packagePath}");
        var result = applier.ApplyPackage(packagePath);
        if (!result.Success)
        {
            Console.Error.WriteLine(result.Message);
            return 1;
        }

        Console.WriteLine($"Updated {result.FromVersion} -> {result.ToVersion}");
        return 0;
    }

    private static int LaunchApp(string appDirectory)
    {
        var exePath = Path.Combine(appDirectory, "LocalPlayer.exe");
        if (!File.Exists(exePath))
        {
            Console.Error.WriteLine($"App not found: {exePath}");
            return 2;
        }

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = appDirectory,
            UseShellExecute = false
        };

        using var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
        {
            Console.Error.WriteLine("Failed to start app.");
            return 3;
        }

        process.WaitForExit();
        return process.ExitCode;
    }

    private static bool TryGetApplyPath(string[] args, out string packagePath)
    {
        packagePath = "";
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--apply", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                packagePath = args[i + 1];
                return true;
            }
        }

        return false;
    }

    private static string? FindPendingPackage(string root)
    {
        var updateDir = Path.Combine(root, "data", "updates");
        if (!Directory.Exists(updateDir))
            return null;

        var candidates = Directory.GetFiles(updateDir, "*.zip")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        foreach (var candidate in candidates)
        {
            try
            {
                using var archive = ZipFile.OpenRead(candidate);
                if (archive.GetEntry("manifest.json") != null)
                    return candidate;
            }
            catch
            {
            }
        }

        return null;
    }
}
