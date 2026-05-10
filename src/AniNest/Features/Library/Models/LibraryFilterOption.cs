using CommunityToolkit.Mvvm.ComponentModel;

namespace AniNest.Features.Library.Models;

public partial class LibraryFilterOption : ObservableObject
{
    public LibraryFilterOption(LibraryFilter filter, string localizationKey, string displayName)
    {
        Filter = filter;
        LocalizationKey = localizationKey;
        _displayName = displayName;
    }

    public LibraryFilter Filter { get; }
    public string LocalizationKey { get; }

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private bool _isSelected;
}
