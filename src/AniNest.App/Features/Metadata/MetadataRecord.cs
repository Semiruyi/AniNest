namespace AniNest.Features.Metadata;

public sealed class MetadataRecord
{
    public string FolderPath { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public string FolderFingerprint { get; set; } = string.Empty;

    public MetadataState State { get; set; } = MetadataState.NeedsMetadata;
    public MetadataFailureKind FailureKind { get; set; } = MetadataFailureKind.None;

    public string? SourceId { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? LastSucceededAtUtc { get; set; }
    public DateTime? CooldownUntilUtc { get; set; }

    public string? MetadataFilePath { get; set; }
    public string? PosterFilePath { get; set; }
}
