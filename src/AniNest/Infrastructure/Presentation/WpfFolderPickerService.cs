namespace AniNest.Infrastructure.Presentation;

public sealed class WpfFolderPickerService : IFolderPickerService
{
    public string? PickFolder(string title)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = title
        };

        return dialog.ShowDialog() == true
            ? dialog.FolderName
            : null;
    }
}
