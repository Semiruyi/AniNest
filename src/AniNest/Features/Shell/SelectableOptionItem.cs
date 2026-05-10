using CommunityToolkit.Mvvm.ComponentModel;

namespace AniNest.Features.Shell;

public partial class SelectableOptionItem : ObservableObject
{
    public SelectableOptionItem(string code, string displayName)
    {
        Code = code;
        _displayName = displayName;
    }

    public string Code { get; }

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private bool _isSelected;
}
