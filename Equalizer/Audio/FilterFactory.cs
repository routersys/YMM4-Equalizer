using Equalizer.Attributes;
using Equalizer.Enums;
using Equalizer.Interfaces;
using System.Collections.Frozen;
using System.Reflection;

namespace Equalizer.Audio;

internal static class FilterFactory
{
    private static readonly FrozenDictionary<EqualizerAlgorithm, Type> Registry = BuildRegistry();

    public static IFilter Create(EqualizerAlgorithm algorithm)
    {
        if (Registry.TryGetValue(algorithm, out var type))
            return (IFilter)Activator.CreateInstance(type)!;

        throw new ArgumentException($"No filter registered for algorithm: {algorithm}");
    }

    private static FrozenDictionary<EqualizerAlgorithm, Type> BuildRegistry()
    {
        var dict = new Dictionary<EqualizerAlgorithm, Type>();

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.IsAbstract || type.IsInterface) continue;
            if (!typeof(IFilter).IsAssignableFrom(type)) continue;

            var attr = type.GetCustomAttribute<FilterAlgorithmAttribute>();
            if (attr is null) continue;

            dict[attr.Algorithm] = type;
        }

        return dict.ToFrozenDictionary();
    }
}