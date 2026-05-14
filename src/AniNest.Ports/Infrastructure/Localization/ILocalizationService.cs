using System.Collections.Generic;
using System.ComponentModel;

namespace AniNest.Infrastructure.Localization;

public interface ILocalizationService : INotifyPropertyChanged
{
    string this[string key] { get; }
    string CurrentLanguage { get; }
    void SetLanguage(string code);
    IReadOnlyList<LanguageInfo> AvailableLanguages { get; }
}
