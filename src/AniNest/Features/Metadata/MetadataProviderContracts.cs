namespace AniNest.Features.Metadata;

public interface IMetadataProvider
{
    Task<MetadataFetchResult> FetchAsync(MetadataFolderRef folder, CancellationToken ct = default);
}

public enum MetadataFetchOutcome
{
    Success,
    NoMatch,
    NetworkError,
    ProviderError
}

public sealed record MetadataFetchResult(
    MetadataFetchOutcome Outcome,
    FolderMetadata? Metadata = null)
{
    public static MetadataFetchResult Success(FolderMetadata metadata)
        => new(MetadataFetchOutcome.Success, metadata);

    public static MetadataFetchResult NoMatch()
        => new(MetadataFetchOutcome.NoMatch);

    public static MetadataFetchResult NetworkError()
        => new(MetadataFetchOutcome.NetworkError);

    public static MetadataFetchResult ProviderError()
        => new(MetadataFetchOutcome.ProviderError);
}
