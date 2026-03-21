using Equalizer.Interfaces;

namespace Equalizer.Services;

public static class ServiceLocator
{
    private static readonly Lazy<IPresetService> LazyPresetService = new(() => new PresetService());
    private static readonly Lazy<IGroupService> LazyGroupService = new(() => new GroupService());

    public static IPresetService PresetService => LazyPresetService.Value;
    public static IGroupService GroupService => LazyGroupService.Value;
}