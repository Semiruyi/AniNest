using AniNest.Application.Resources;
using AniNest.Host.Modules.Resources;

namespace AniNest.Host.Endpoints;

internal static class ResourceEndpoints
{
    public static IEndpointRouteBuilder MapResourceEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/resources").WithTags("Resources");

        group.MapGet("/{kind}/{ownerId}", async (
            string kind,
            string ownerId,
            IResourceLocator locator,
            CancellationToken cancellationToken) =>
        {
            if (!ResourceKindCodec.TryParse(kind, out var resourceKind))
            {
                return Results.NotFound();
            }

            var resource = await locator.ResolveAsync(
                new ResourceKey(resourceKind, ownerId),
                cancellationToken);

            if (resource is null)
            {
                return Results.NotFound();
            }

            return Results.File(
                resource.FilePath,
                contentType: resource.ContentType,
                enableRangeProcessing: true);
        });

        return app;
    }
}
