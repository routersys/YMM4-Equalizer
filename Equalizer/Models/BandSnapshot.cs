using Equalizer.Enums;

namespace Equalizer.Models;

public readonly record struct BandSnapshot(
    bool IsEnabled,
    FilterType Type,
    StereoMode StereoMode,
    float Frequency,
    float Gain,
    float Q
);