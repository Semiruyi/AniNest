using AniNest.Core.Enums;

namespace AniNest.Application.Metadata;

public sealed record MetadataRecord(
    string FolderId,
    string FolderPath,
    string FolderName,
    string FolderFingerprint,
    MetadataState State,
    MetadataFailureKind FailureKind,
    string? SourceId,
    DateTime? LastAttemptAtUtc,
    DateTime? LastSucceededAtUtc,
    DateTime? CooldownUntilUtc,
    string? MetadataFilePath,
    string? PosterFilePath);
