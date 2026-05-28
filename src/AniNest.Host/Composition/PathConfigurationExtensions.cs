namespace AniNest.Host.Composition;

internal static class PathConfigurationExtensions
{
    public static string ResolveAniNestPath(this IConfiguration configuration, string key, string fileName)
    {
        var configured = configuration[key];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.IsPathRooted(configured)
                ? configured
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured));
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data", fileName));
    }
}
