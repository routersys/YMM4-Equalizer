using Equalizer.Enums;

namespace Equalizer.Audio;

internal interface IFilter
{
    void Reset();
    void SetCoefficients(FilterType type, float sampleRate, float freq, float gainDb, float q);
    float Process(float input);
}