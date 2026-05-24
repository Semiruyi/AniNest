namespace AniNest.Application.Metadata;

public enum MetadataTaskReason
{
    MissingMetadata = 0,
    LibraryImport = 1,
    LibraryReconcile = 2,
    ManualRefresh = 3,
    RetryFailed = 4
}
