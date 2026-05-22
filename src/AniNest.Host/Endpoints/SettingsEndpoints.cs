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

        return app;
    }
}
