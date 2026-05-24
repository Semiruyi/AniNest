using AniNest.Application.Resources;

namespace AniNest.Host.Modules.Resources;

internal sealed class ResourceUrlService : IResourceUrlService
{
    public string GetUrl(ResourceKey key)
    {
        var kindSegment = ResourceKindCodec.ToRouteSegment(key.Kind);
        var ownerId = Uri.EscapeDataString(key.OwnerId);
        return $"/api/resources/{kindSegment}/{ownerId}";
    }
}
