using System.Text.Json;
using AniNest.Application.Playback;

namespace AniNest.Host.Modules;

internal sealed class FilePlaybackProgressStore : IPlaybackProgressStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _progressPath;
    private readonly PlaybackProgressDocument _defaults;
    private readonly object _sync = new();
    private PlaybackProgressDocument? _cached;

    public FilePlaybackProgressStore(
        string progressPath,
        IReadOnlyList<VideoProgressState> defaultVideoProgress,
        IReadOnlyList<FolderProgressState> defaultFolderProgress)
    {
        _progressPath = progressPath;
        _defaults = new PlaybackProgressDocument
        {
            VideoProgress = defaultVideoProgress.ToDictionary(item => item.FilePath, StringComparer.OrdinalIgnoreCase),
            FolderProgress = defaultFolderProgress.ToDictionary(item => item.FolderId, StringComparer.OrdinalIgnoreCase)
        };
    }

    public VideoProgressState? GetVideoProgress(string filePath)
    {
        var document = LoadDocument();
        return document.VideoProgress.TryGetValue(filePath, out var progress) ? progress : null;
    }

    public void SaveVideoProgress(string filePath, long position, long duration)
    {
        var document = LoadDocument();
        document.VideoProgress[filePath] = new VideoProgressState(filePath, position, duration, false);
        SaveDocument(document);
    }

    public void MarkVideoPlayed(string filePath)
    {
        var document = LoadDocument();
        if (document.VideoProgress.TryGetValue(filePath, out var existing))
        {
            document.VideoProgress[filePath] = existing with { Position = 0, IsPlayed = true };
        }
        else
        {
            document.VideoProgress[filePath] = new VideoProgressState(filePath, 0, 0, true);
        }

        SaveDocument(document);
    }

    public FolderProgressState? GetFolderProgress(string folderId)
    {
        var document = LoadDocument();
        return document.FolderProgress.TryGetValue(folderId, out var progress) ? progress : null;
    }

    public void SaveFolderProgress(string folderId, string lastItemId)
    {
        var document = LoadDocument();
        document.FolderProgress[folderId] = new FolderProgressState(folderId, lastItemId);
        SaveDocument(document);
    }

    public PlaybackSessionState? GetLastSession()
    {
        var document = LoadDocument();
        return document.LastSession;
    }

    public void SaveLastSession(PlaybackSessionState session)
    {
        var document = LoadDocument();
        document.LastSession = session;
        SaveDocument(document);
    }

    public void ClearLastSession()
    {
        var document = LoadDocument();
        document.LastSession = null;
        SaveDocument(document);
    }

    private PlaybackProgressDocument LoadDocument()
    {
        lock (_sync)
        {
            if (_cached is not null)
                return _cached;

            if (!File.Exists(_progressPath))
            {
                _cached = Clone(_defaults);
                return _cached;
            }

            using var stream = File.OpenRead(_progressPath);
            _cached = JsonSerializer.Deserialize<PlaybackProgressDocument>(stream, SerializerOptions) ?? Clone(_defaults);
            Normalize(_cached);
            return _cached;
        }
    }

    private void SaveDocument(PlaybackProgressDocument document)
    {
        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_progressPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            Normalize(document);
            using var stream = File.Create(_progressPath);
            JsonSerializer.Serialize(stream, document, SerializerOptions);
            _cached = Clone(document);
        }
    }

    private static void Normalize(PlaybackProgressDocument document)
    {
        document.VideoProgress ??= new Dictionary<string, VideoProgressState>(StringComparer.OrdinalIgnoreCase);
        document.FolderProgress ??= new Dictionary<string, FolderProgressState>(StringComparer.OrdinalIgnoreCase);
    }

    private static PlaybackProgressDocument Clone(PlaybackProgressDocument source)
    {
        return new PlaybackProgressDocument
        {
            VideoProgress = new Dictionary<string, VideoProgressState>(source.VideoProgress, StringComparer.OrdinalIgnoreCase),
            FolderProgress = new Dictionary<string, FolderProgressState>(source.FolderProgress, StringComparer.OrdinalIgnoreCase),
            LastSession = source.LastSession
        };
    }

    private sealed class PlaybackProgressDocument
    {
        public Dictionary<string, VideoProgressState> VideoProgress { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, FolderProgressState> FolderProgress { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public PlaybackSessionState? LastSession { get; set; }
    }
}
