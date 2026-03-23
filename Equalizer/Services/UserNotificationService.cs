using Equalizer.Interfaces;
using Equalizer.Views;
using System.Windows;

namespace Equalizer.Services;

public sealed class UserNotificationService : IUserNotificationService
{
    private sealed record CooldownKey(NotificationSeverity Severity, string Message);

    private static readonly TimeSpan ErrorCooldown = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan WarningCooldown = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan InfoCooldown = TimeSpan.FromSeconds(2);
    private const int MaxCooldownEntries = 500;

    private readonly ToastManager _toastManager;
    private readonly Dictionary<CooldownKey, DateTime> _cooldownMap = new(MaxCooldownEntries);
    private readonly Lock _cooldownLock = new();

    public UserNotificationService(IToastPresenter presenter)
    {
        _toastManager = new ToastManager(presenter);
    }

    public void ShowError(string message) =>
        TryDispatch(NotificationSeverity.Error, message, ErrorCooldown);

    public void ShowWarning(string message) =>
        TryDispatch(NotificationSeverity.Warning, message, WarningCooldown);

    public void ShowInfo(string message) =>
        TryDispatch(NotificationSeverity.Info, message, InfoCooldown);

    public Task<bool> ConfirmAsync(string message, string title)
    {
        if (Application.Current.Dispatcher.CheckAccess())
            return Task.FromResult(ExecuteConfirmDialog(message, title));

        return Application.Current.Dispatcher.InvokeAsync(
            () => ExecuteConfirmDialog(message, title)).Task;
    }

    public Task<string?> PromptAsync(string message, string title, string defaultText = "")
    {
        if (Application.Current.Dispatcher.CheckAccess())
            return Task.FromResult(ExecutePromptDialog(message, title, defaultText));

        return Application.Current.Dispatcher.InvokeAsync(
            () => ExecutePromptDialog(message, title, defaultText)).Task;
    }

    private static bool ExecuteConfirmDialog(string message, string title)
    {
        var dialog = new ConfirmationDialogWindow(message, title)
        {
            Owner = ResolveOwnerWindow()
        };
        return dialog.ShowDialog() == true;
    }

    private static string? ExecutePromptDialog(string message, string title, string defaultText)
    {
        var dialog = new InputDialogWindow(message, title, defaultText)
        {
            Owner = ResolveOwnerWindow()
        };
        return dialog.ShowDialog() == true ? dialog.InputText : null;
    }

    private void TryDispatch(NotificationSeverity severity, string message, TimeSpan cooldown)
    {
        var key = new CooldownKey(severity, message);
        var now = DateTime.UtcNow;

        lock (_cooldownLock)
        {
            if (_cooldownMap.TryGetValue(key, out DateTime lastShown) && now - lastShown < cooldown)
                return;

            _cooldownMap[key] = now;
            PruneEntries(now);
        }

        Application.Current.Dispatcher.BeginInvoke(() => _toastManager.Push(severity, message));
    }

    private void PruneEntries(DateTime now)
    {
        var expired = _cooldownMap
            .Where(kvp => now - kvp.Value > ErrorCooldown)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expired)
            _cooldownMap.Remove(key);

        if (_cooldownMap.Count <= MaxCooldownEntries) return;

        var oldest = _cooldownMap
            .OrderBy(kvp => kvp.Value)
            .Take(_cooldownMap.Count - MaxCooldownEntries / 2)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldest)
            _cooldownMap.Remove(key);
    }

    private static Window ResolveOwnerWindow() =>
        Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive)
        ?? Application.Current.MainWindow;
}