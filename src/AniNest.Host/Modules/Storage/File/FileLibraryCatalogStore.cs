using System.Text.Json;
using AniNest.Application.Library;
using AniNest.Core.Enums;

namespace AniNest.Host.Modules;

internal sealed class FileLibraryCatalogStore : ILibraryCatalogStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _catalogPath;
    private readonly LibraryCatalogDocument _defaults;
    private readonly object _sync = new();
    private LibraryCatalogDocument? _cached;

    public FileLibraryCatalogStore(
        string catalogPath,
        IReadOnlyList<LibraryFolderRecord> defaultFolders,
        IReadOnlyDictionary<string, WatchStatus> defaultWatchStatuses,
        IReadOnlyDictionary<string, bool> defaultFavorites)
    {
        _catalogPath = catalogPath;
        _defaults = new LibraryCatalogDocument
        {
            Folders = defaultFolders.ToList(),
            WatchStatuses = new Dictionary<string, WatchStatus>(defaultWatchStatuses, StringComparer.OrdinalIgnoreCase),
            Favorites = new Dictionary<string, bool>(defaultFavorites, StringComparer.OrdinalIgnoreCase)
        };
    }

    public IReadOnlyList<LibraryFolderRecord> GetFolders()
        => LoadDocument().Folders.ToArray();

    public void SaveFolders(IReadOnlyList<LibraryFolderRecord> folders)
    {
        var document = LoadDocument();
        document.Folders = folders.ToList();
        SaveDocument(document);
    }

    public WatchStatus GetWatchStatus(string folderId)
    {
        var document = LoadDocument();
        return document.WatchStatuses.TryGetValue(folderId, out var status)
            ? status
            : WatchStatus.Unknown;
    }

    public void SetWatchStatus(string folderId, WatchStatus status)
    {
        var document = LoadDocument();
        document.WatchStatuses[folderId] = status;
        SaveDocument(document);
    }

    public bool GetIsFavorite(string folderId)
    {
        var document = LoadDocument();
        return document.Favorites.TryGetValue(folderId, out var isFavorite) && isFavorite;
    }

    public void SetIsFavorite(string folderId, bool isFavorite)
    {
        var document = LoadDocument();
        document.Favorites[folderId] = isFavorite;
        SaveDocument(document);
    }

    private LibraryCatalogDocument LoadDocument()
    {
        lock (_sync)
        {
            if (_cached is not null)
                return _cached;

            if (!File.Exists(_catalogPath))
            {
                _cached = Clone(_defaults);
                return _cached;
            }

            using var stream = File.OpenRead(_catalogPath);
            _cached = JsonSerializer.Deserialize<LibraryCatalogDocument>(stream, SerializerOptions) ?? Clone(_defaults);
            Normalize(_cached);
            return _cached;
        }
    }

    private void SaveDocument(LibraryCatalogDocument document)
    {
        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_catalogPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            Normalize(document);
            using var stream = File.Create(_catalogPath);
            JsonSerializer.Serialize(stream, document, SerializerOptions);
            _cached = Clone(document);
        }
    }

    private static void Normalize(LibraryCatalogDocument document)
    {
        document.WatchStatuses ??= new Dictionary<string, WatchStatus>(StringComparer.OrdinalIgnoreCase);
        document.Favorites ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        document.Folders ??= [];
    }

    private static LibraryCatalogDocument Clone(LibraryCatalogDocument source)
    {
        return new LibraryCatalogDocument
        {
            Folders = source.Folders.ToList(),
            WatchStatuses = new Dictionary<string, WatchStatus>(source.WatchStatuses, StringComparer.OrdinalIgnoreCase),
            Favorites = new Dictionary<string, bool>(source.Favorites, StringComparer.OrdinalIgnoreCase)
        };
    }

    private sealed class LibraryCatalogDocument
    {
        public List<LibraryFolderRecord> Folders { get; set; } = [];
        public Dictionary<string, WatchStatus> WatchStatuses { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, bool> Favorites { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
