using System.Text.Json;
using AniNest.Application.Settings;
using AniNest.Contracts.Settings;

namespace AniNest.Host.Modules;

internal sealed class FileSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly AppSettingsDto _defaultSettings;
    private readonly object _sync = new();
    private AppSettingsDto? _cached;

    public FileSettingsStore(string settingsPath, AppSettingsDto defaultSettings)
    {
        _settingsPath = settingsPath;
        _defaultSettings = defaultSettings;
    }

    public AppSettingsDto Load()
    {
        lock (_sync)
        {
            if (_cached is not null)
                return _cached;

            if (!File.Exists(_settingsPath))
            {
                _cached = _defaultSettings;
                return _cached;
            }

            using var stream = File.OpenRead(_settingsPath);
            _cached = JsonSerializer.Deserialize<AppSettingsDto>(stream, SerializerOptions) ?? _defaultSettings;
            return _cached;
        }
    }

    public void Save(AppSettingsDto settings)
    {
        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            using var stream = File.Create(_settingsPath);
            JsonSerializer.Serialize(stream, settings, SerializerOptions);
            _cached = settings;
        }
    }
}
