using System.Runtime.CompilerServices;
using Equalizer.Enums;
using Equalizer.Attributes;

namespace Equalizer.Audio;

[FilterAlgorithm(EqualizerAlgorithm.TPT_SVF)]
internal sealed class TptSvfFilter : IFilter
{
    private float _s1, _s2;
    private float _g, _twoRPlusG, _den;
    private float _kHP, _kBP, _kLP;

    public void Reset()
    {
        _s1 = 0f;
        _s2 = 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCoefficients(FilterType type, float sampleRate, float freq, float gainDb, float q)
    {
        freq = Math.Clamp(freq, 20f, sampleRate * 0.5f - 1f);
        q = Math.Max(q, 0.01f);

        _g = MathF.Tan(MathF.PI * freq / sampleRate);
        float invQ = 1f / q;
        _den = 1f / (1f + invQ * _g + _g * _g);
        _twoRPlusG = invQ + _g;

        float linearGain = MathF.Pow(10f, gainDb / 20f);
        float mix = linearGain - 1f;

        _kHP = 0f;
        _kBP = 0f;
        _kLP = 0f;

        switch (type)
        {
            case FilterType.Peak: _kBP = mix; break;
            case FilterType.LowShelf: _kLP = mix; break;
            case FilterType.HighShelf: _kHP = mix; break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Process(float input)
    {
        float hp = (input - _twoRPlusG * _s1 - _s2) * _den;
        float bp = _g * hp + _s1;
        float lp = _g * bp + _s2;

        _s1 = _g * hp + bp;
        _s2 = _g * bp + lp;

        return input + _kHP * hp + _kBP * bp + _kLP * lp;
    }
}