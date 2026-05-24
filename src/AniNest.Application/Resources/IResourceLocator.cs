namespace AniNest.Application.Resources;

public interface IResourceLocator
{
    Task<ResolvedResource?> ResolveAsync(
        ResourceKey key,
        CancellationToken cancellationToken = default);
}
