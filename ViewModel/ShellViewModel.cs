using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using LocalPlayer.Messages;
using LocalPlayer.View.Library;
using LocalPlayer.View.Player;

namespace LocalPlayer.ViewModel;

public partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    [ObservableProperty]
    private object? _currentPage;

    public ShellViewModel(IServiceProvider services)
    {
        _services = services;

        WeakReferenceMessenger.Default.Register<FolderSelectedMessage>(this, (_, m) =>
        {
            CurrentPage = _services.GetRequiredService<PlayerPage>();
            WeakReferenceMessenger.Default.Send(new LoadPlayerFolderMessage(m.Path, m.Name));
        });

        WeakReferenceMessenger.Default.Register<BackRequestedMessage>(this, (_, _) =>
        {
            CurrentPage = _services.GetRequiredService<MainPage>();
        });

        CurrentPage = _services.GetRequiredService<MainPage>();
    }
}
