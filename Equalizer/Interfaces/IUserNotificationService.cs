namespace Equalizer.Interfaces;

public interface IUserNotificationService
{
    void ShowError(string message);
    void ShowWarning(string message);
    void ShowInfo(string message);
    Task<bool> ConfirmAsync(string message, string title);
    Task<string?> PromptAsync(string message, string title, string defaultText = "");
}