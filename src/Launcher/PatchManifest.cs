using System.Text.Json.Serialization;

namespace AniNest.Launcher;

public sealed class PatchManifest
{
    [JsonPropertyName("appId")]
    public string AppId { get; set; } = "AniNest";

    [JsonPropertyName("packageType")]
    public string PackageType { get; set; } = "patch";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("baseVersion")]
    public string BaseVersion { get; set; } = "";

    [JsonPropertyName("generatedAtUtc")]
    public DateTime GeneratedAtUtc { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("files")]
    public List<PatchFileEntry> Files { get; set; } = [];
}

public sealed class PatchFileEntry
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "replace";

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }
}
