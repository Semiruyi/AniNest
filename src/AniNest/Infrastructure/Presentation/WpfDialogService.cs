using System.Windows;

namespace AniNest.Infrastructure.Presentation;

public sealed class WpfDialogService : IDialogService
{
    public bool ShowConfirmation(string message, string title)
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string ShowInput(string prompt, string title, string defaultValue)
        => Microsoft.VisualBasic.Interaction.InputBox(prompt, title, defaultValue);

    public void ShowInfo(string message, string title)
        => MessageBox.Show(message, title);

    public void ShowError(string message, string title)
        => MessageBox.Show(message, title);
}
