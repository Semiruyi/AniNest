using AniNest.Application.Modules;
using AniNest.Contracts.Settings;

namespace AniNest.Host.Endpoints;

internal static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings").WithTags("Settings");

        group.MapGet("/", (ISettingsModule module, CancellationToken cancellationToken)
            => module.GetAsync(cancellationToken));

        group.MapPut("/", async (AppSettingsDto request, ISettingsModule module, CancellationToken cancellationToken) =>
        {
            await module.SaveAsync(request, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/player", (ISettingsModule module, CancellationToken cancellationToken)
            => module.GetPlayerAsync(cancellationToken));

        group.MapPut("/player", async (PlayerSettingsDto request, ISettingsModule module, CancellationToken cancellationToken) =>
        {
            await module.SavePlayerAsync(request, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/metadata", (ISettingsModule module, CancellationToken cancellationToken)
            => module.GetMetadataAsync(cancellationToken));

        group.MapPut("/metadata", async (MetadataSettingsDto request, ISettingsModule module, CancellationToken cancellationToken) =>
        {
            await module.SaveMetadataAsync(request, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/thumbnails", (ISettingsModule module, CancellationToken cancellationToken)
            => module.GetThumbnailsAsync(cancellationToken));

        group.MapPut("/thumbnails", async (ThumbnailSettingsDto request, ISettingsModule module, CancellationToken cancellationToken) =>
        {
            await module.SaveThumbnailsAsync(request, cancellationToken);
            return Results.NoContent();
        });

        return app;
    }
}
