using System.IO;
using System.IO.Compression;
using System.Text.Json;
using FluentAssertions;
using AniNest.Launcher;
using Xunit;

namespace AniNest.Tests.Launcher;

public class PatchApplierTests : IDisposable
{
    private readonly string _root;

    public PatchApplierTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"AniNestLauncherTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_root, "app"));
        Directory.CreateDirectory(Path.Combine(_root, "data", "config"));
        Directory.CreateDirectory(Path.Combine(_root, "data", "logs"));

        File.WriteAllText(Path.Combine(_root, "app", "manifest.json"), JsonSerializer.Serialize(new PatchManifest
        {
            AppId = "AniNest",
            PackageType = "full",
            Version = "1.0.0",
            GeneratedAtUtc = DateTime.UtcNow
        }));
        File.WriteAllText(Path.Combine(_root, "app", "keep.txt"), "old");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    [Fact]
    public void ApplyPackage_UpdatesAppAndKeepsData()
    {
        var package = CreatePatchPackage(
            "1.0.0",
            "1.0.1",
            [
                new PatchFileEntry { Path = "keep.txt", Action = "replace", Sha256 = HashOf("new") },
                new PatchFileEntry { Path = "added.txt", Action = "add", Sha256 = HashOf("added") }
            ],
            new Dictionary<string, string>
            {
                ["keep.txt"] = "new",
                ["added.txt"] = "added"
            });

        var result = new PatchApplier(_root).ApplyPackage(package);

        result.Success.Should().BeTrue(result.Message);
        File.ReadAllText(Path.Combine(_root, "app", "keep.txt")).Should().Be("new");
        File.ReadAllText(Path.Combine(_root, "app", "added.txt")).Should().Be("added");
        Directory.Exists(Path.Combine(_root, "data", "config")).Should().BeTrue();
        var manifest = JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(Path.Combine(_root, "app", "manifest.json")));
        manifest!.Version.Should().Be("1.0.1");
    }

    [Fact]
    public void ApplyPackage_BaseVersionMismatch_Fails()
    {
        var package = CreatePatchPackage(
            "2.0.0",
            "2.0.1",
            [new PatchFileEntry { Path = "keep.txt", Action = "replace", Sha256 = HashOf("new") }],
            new Dictionary<string, string> { ["keep.txt"] = "new" });

        var result = new PatchApplier(_root).ApplyPackage(package);

        result.Success.Should().BeFalse();
        File.ReadAllText(Path.Combine(_root, "app", "keep.txt")).Should().Be("old");
    }

    [Fact]
    public void ApplyPackage_DoesNotTouchZipBesideLauncherContract()
    {
        var package = CreatePatchPackage(
            "1.0.0",
            "1.0.1",
            [new PatchFileEntry { Path = "keep.txt", Action = "replace", Sha256 = HashOf("new") }],
            new Dictionary<string, string> { ["keep.txt"] = "new" });

        var result = new PatchApplier(_root).ApplyPackage(package);

        result.Success.Should().BeTrue();
        File.Exists(package).Should().BeTrue();
    }

    private string CreatePatchPackage(string baseVersion, string version, IEnumerable<PatchFileEntry> files, Dictionary<string, string> payloads)
    {
        var zipPath = Path.Combine(_root, $"patch_{Guid.NewGuid():N}.zip");
        var manifest = new PatchManifest
        {
            AppId = "AniNest",
            PackageType = "patch",
            Version = version,
            BaseVersion = baseVersion,
            GeneratedAtUtc = DateTime.UtcNow,
            Files = files.ToList()
        };

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var manifestEntry = zip.CreateEntry("manifest.json");
            using (var stream = manifestEntry.Open())
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(JsonSerializer.Serialize(manifest));
            }

            foreach (var payload in payloads)
            {
                var entry = zip.CreateEntry(payload.Key.Replace('\\', '/'));
                using var stream = entry.Open();
                using var writer = new StreamWriter(stream);
                writer.Write(payload.Value);
            }
        }

        return zipPath;
    }

    private static string HashOf(string text)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
