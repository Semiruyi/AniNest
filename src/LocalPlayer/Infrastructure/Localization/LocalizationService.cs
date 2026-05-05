using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using LocalPlayer.Infrastructure.Paths;

namespace LocalPlayer.Infrastructure.Localization;

public class LocalizationService : ILocalizationService
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance!;

    private readonly string _languagesDir;
    private Dictionary<string, string> _entries = new();
    private string _currentLanguage = "zh-CN";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentLanguage => _currentLanguage;

    public string this[string key]
    {
        get
        {
            if (_entries.TryGetValue(key, out var value))
                return value;
            return key;
        }
    }

    public IReadOnlyList<LanguageInfo> AvailableLanguages { get; private set; } = Array.Empty<LanguageInfo>();

    public LocalizationService()
    {
        _instance = this;
        _languagesDir = AppPaths.LanguagesDirectory;
        ScanLanguages();
    }

    public void SetLanguage(string code)
    {
        var filePath = Path.Combine(_languagesDir, $"{code}.json");
        if (!File.Exists(filePath))
            return;

        var json = File.ReadAllText(filePath);
        _entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        _currentLanguage = code;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    private void ScanLanguages()
    {
        if (!Directory.Exists(_languagesDir))
            return;

        var files = Directory.GetFiles(_languagesDir, "*.json");
        var list = new List<LanguageInfo>();

        foreach (var file in files)
        {
            var code = Path.GetFileNameWithoutExtension(file);
            var name = code;

            try
            {
                var json = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("_languageName", out var nameProp))
                    name = nameProp.GetString() ?? code;
            }
            catch
            {
                // keep code as name
            }

            list.Add(new LanguageInfo(code, name));
        }

        AvailableLanguages = list;
    }
}
