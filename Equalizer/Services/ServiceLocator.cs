using Equalizer.Interfaces;

namespace Equalizer.Services;

public static class ServiceLocator
{
    private static volatile IToastPresenter? _registeredPresenter;

    private static readonly Lazy<IUserNotificationService> LazyNotificationService =
        new(() => new UserNotificationService(
            _registeredPresenter
            ?? throw new InvalidOperationException(
                $"{nameof(RegisterToastPresenter)} must be called before first notification use.")));

    private static readonly Lazy<IPresetService> LazyPresetService =
        new(() => new PresetService(LazyNotificationService.Value));

    private static readonly Lazy<IGroupService> LazyGroupService =
        new(() => new GroupService());

    private static readonly Lazy<IWindowThemeService> LazyWindowThemeService =
        new(() => new WindowThemeService());

    private static readonly Lazy<IEffectTrackerService> LazyEffectTrackerService =
        new(() => new EffectTrackerService());

    public static void RegisterToastPresenter(IToastPresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(presenter);

        if (Interlocked.CompareExchange(ref _registeredPresenter, presenter, null) is not null)
            throw new InvalidOperationException($"{nameof(RegisterToastPresenter)} must only be called once.");
    }

    public static IUserNotificationService NotificationService => LazyNotificationService.Value;
    public static IPresetService PresetService => LazyPresetService.Value;
    public static IGroupService GroupService => LazyGroupService.Value;
    public static IWindowThemeService WindowThemeService => LazyWindowThemeService.Value;
    public static IEffectTrackerService EffectTrackerService => LazyEffectTrackerService.Value;
}