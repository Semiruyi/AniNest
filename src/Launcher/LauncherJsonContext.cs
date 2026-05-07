using System.Text.Json.Serialization;

namespace LocalPlayer.Launcher;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
[JsonSerializable(typeof(PatchManifest))]
internal sealed partial class LauncherJsonContext : JsonSerializerContext
{
}
