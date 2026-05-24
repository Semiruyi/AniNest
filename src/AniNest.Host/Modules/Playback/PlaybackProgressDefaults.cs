namespace AniNest.Host.Modules;

internal static class PlaybackProgressDefaults
{
    public static IReadOnlyList<AniNest.Application.Playback.VideoProgressState> CreateVideoProgress()
        => Array.Empty<AniNest.Application.Playback.VideoProgressState>();

    public static IReadOnlyList<AniNest.Application.Playback.FolderProgressState> CreateFolderProgress()
        => Array.Empty<AniNest.Application.Playback.FolderProgressState>();
}
