using AniNest.Host.Composition;
using AniNest.Host.Endpoints;
using AniNest.Host.ErrorHandling;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAniNestHostServices(builder.Configuration);

var app = builder.Build();

app.UseApiExceptionHandling();
app.MapGet("/", () => Results.Redirect("/api/settings"));
app.MapLibraryEndpoints();
app.MapPlaylistEndpoints();
app.MapSessionEndpoints();
app.MapMetadataEndpoints();
app.MapThumbnailEndpoints();
app.MapSettingsEndpoints();
app.MapEventEndpoints();

app.Run();

public partial class Program;
