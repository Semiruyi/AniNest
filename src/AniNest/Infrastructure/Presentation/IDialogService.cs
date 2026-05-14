namespace AniNest.Infrastructure.Presentation;

public interface IDialogService
{
    string ShowInput(string prompt, string title, string defaultValue);
    void ShowInfo(string message, string title);
    void ShowError(string message, string title);
}
