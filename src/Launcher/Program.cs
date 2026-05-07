namespace LocalPlayer.Launcher;

internal static class Program
{
    private static int Main(string[] args)
    {
        var root = AppContext.BaseDirectory;
        var applier = new PatchApplier(root);

        if (TryGetApplyPath(args, out var packagePath))
        {
            var code = ApplyPackageAndReport(applier, packagePath);
            if (code != 0)
                return code;

            DeleteAppliedPackage(packagePath);
            return LaunchApp(root);
        }

        var pendingPackage = LauncherPackageLocator.FindPendingPackage(root);
        if (!string.IsNullOrWhiteSpace(pendingPackage))
        {
            var code = ApplyPackageAndReport(applier, pendingPackage);
            if (code != 0)
                return code;

            DeleteAppliedPackage(pendingPackage);
        }

        return LaunchApp(root);
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

    private static int LaunchApp(string root)
    {
        var appDirectory = LauncherPaths.ResolveAppDirectory(root);
        var exePath = Path.Combine(appDirectory, "AniNest.exe");
        if (!File.Exists(exePath))
        {
            Console.Error.WriteLine($"App not found. Resolved app directory: {appDirectory}");
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

    private static void DeleteAppliedPackage(string packagePath)
    {
        try
        {
            if (File.Exists(packagePath))
                File.Delete(packagePath);
        }
        catch
        {
        }
    }
}
