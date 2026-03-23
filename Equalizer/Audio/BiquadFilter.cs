using Equalizer.Attributes;
using Equalizer.Enums;
using Equalizer.Interfaces;
using System.Runtime.CompilerServices;

namespace Equalizer.Audio;

[FilterAlgorithm(EqualizerAlgorithm.Biquad)]
internal sealed class BiquadFilter : IFilter
{
    private float _a1, _a2, _b0, _b1, _b2;
    private float _x1, _x2, _y1, _y2;

    public void Reset()
    {
        _x1 = _x2 = _y1 = _y2 = 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCoefficients(FilterType type, float sampleRate, float freq, float gainDb, float q)
    {
        freq = Math.Clamp(freq, 20f, sampleRate * 0.5f - 1f);
        q = Math.Max(q, 0.01f);

        float omega = 2f * MathF.PI * freq / sampleRate;
        float sinOmega = MathF.Sin(omega);
        float cosOmega = MathF.Cos(omega);
        float alpha = sinOmega / (2f * q);
        float aVal = MathF.Pow(10f, gainDb / 40f);

        float b0, b1, b2, a0, a1, a2;

        switch (type)
        {
            case FilterType.Peak:
                b0 = 1f + alpha * aVal;
                b1 = -2f * cosOmega;
                b2 = 1f - alpha * aVal;
                a0 = 1f + alpha / aVal;
                a1 = -2f * cosOmega;
                a2 = 1f - alpha / aVal;
                break;

            case FilterType.LowShelf:
                {
                    float sqrtA = MathF.Sqrt(aVal);
                    float twoSqrtAAlpha = 2f * sqrtA * alpha;
                    b0 = aVal * ((aVal + 1f) - (aVal - 1f) * cosOmega + twoSqrtAAlpha);
                    b1 = 2f * aVal * ((aVal - 1f) - (aVal + 1f) * cosOmega);
                    b2 = aVal * ((aVal + 1f) - (aVal - 1f) * cosOmega - twoSqrtAAlpha);
                    a0 = (aVal + 1f) + (aVal - 1f) * cosOmega + twoSqrtAAlpha;
                    a1 = -2f * ((aVal - 1f) + (aVal + 1f) * cosOmega);
                    a2 = (aVal + 1f) + (aVal - 1f) * cosOmega - twoSqrtAAlpha;
                    break;
                }

            case FilterType.HighShelf:
                {
                    float sqrtA = MathF.Sqrt(aVal);
                    float twoSqrtAAlpha = 2f * sqrtA * alpha;
                    b0 = aVal * ((aVal + 1f) + (aVal - 1f) * cosOmega + twoSqrtAAlpha);
                    b1 = -2f * aVal * ((aVal - 1f) + (aVal + 1f) * cosOmega);
                    b2 = aVal * ((aVal + 1f) + (aVal - 1f) * cosOmega - twoSqrtAAlpha);
                    a0 = (aVal + 1f) - (aVal - 1f) * cosOmega + twoSqrtAAlpha;
                    a1 = 2f * ((aVal - 1f) - (aVal + 1f) * cosOmega);
                    a2 = (aVal + 1f) - (aVal - 1f) * cosOmega - twoSqrtAAlpha;
                    break;
                }

            default:
                b0 = 1f; b1 = 0f; b2 = 0f;
                a0 = 1f; a1 = 0f; a2 = 0f;
                break;
        }

        float invA0 = 1f / a0;
        _b0 = b0 * invA0;
        _b1 = b1 * invA0;
        _b2 = b2 * invA0;
        _a1 = a1 * invA0;
        _a2 = a2 * invA0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Process(float input)
    {
        float output = _b0 * input + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
        _x2 = _x1;
        _x1 = input;
        _y2 = _y1;
        _y1 = output;
        return output;
    }
}