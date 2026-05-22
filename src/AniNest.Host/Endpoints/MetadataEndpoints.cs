namespace AniNest.Host.Endpoints;

internal static class MetadataEndpoints
{
    public static IEndpointRouteBuilder MapMetadataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/metadata").WithTags("Metadata");

        group.MapGet("/status-summary", () => Results.StatusCode(StatusCodes.Status501NotImplemented));

        return app;
    }
}
