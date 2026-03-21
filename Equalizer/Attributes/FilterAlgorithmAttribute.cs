using Equalizer.Enums;

namespace Equalizer.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
internal sealed class FilterAlgorithmAttribute(EqualizerAlgorithm algorithm) : Attribute
{
    public EqualizerAlgorithm Algorithm { get; } = algorithm;
}