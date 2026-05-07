using CommunityToolkit.Mvvm.ComponentModel;
using AniNest.Features.Player.Input;

namespace AniNest.Features.Player.Settings;

public partial class PlayerInputBindingItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _actionDisplayName = "";

    [ObservableProperty]
    private string _bindingDisplay = "";

    [ObservableProperty]
    private bool _isCapturing;

    public int Index { get; init; }
    public PlayerInputAction Action { get; init; }
}
