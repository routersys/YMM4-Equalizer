using System.Runtime.CompilerServices;

namespace Equalizer.Audio;

internal sealed class OverSampler
{
    private float _lastIn;
    private float _lastOut;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Upsample(float input, out float out1, out float out2)
    {
        out1 = (_lastIn + input) * 0.5f;
        out2 = input;
        _lastIn = input;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Downsample(float input1, float input2)
    {
        float output = (_lastOut + input1) * 0.5f;
        _lastOut = input2;
        return output;
    }

    public void Reset()
    {
        _lastIn = 0f;
        _lastOut = 0f;
    }
}