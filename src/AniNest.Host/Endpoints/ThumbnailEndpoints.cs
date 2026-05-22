namespace AniNest.Host.Endpoints;

internal static class ThumbnailEndpoints
{
    public static IEndpointRouteBuilder MapThumbnailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/thumbnails").WithTags("Thumbnails");

        group.MapGet("/videos/{videoId}", () => Results.StatusCode(StatusCodes.Status501NotImplemented));

        return app;
    }
}
