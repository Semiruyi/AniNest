namespace AniNest.Application.Playback;

public sealed record PlaybackFolderProgressSummary(
    int PlayedCount);

public sealed class PlaybackProgressSummaryService
{
    private readonly IPlaybackProgressStore _store;

    public PlaybackProgressSummaryService(IPlaybackProgressStore store)
    {
        _store = store;
    }

    public PlaybackFolderProgressSummary SummarizeFolder(IEnumerable<string> videoFiles)
    {
        var playedCount = videoFiles.Count(filePath =>
            _store.GetVideoProgress(filePath)?.IsPlayed == true);

        return new PlaybackFolderProgressSummary(playedCount);
    }
}
