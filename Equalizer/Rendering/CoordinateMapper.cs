namespace Equalizer.Rendering;

internal readonly struct CoordinateMapper(
    double width,
    double height,
    double minFreq,
    double maxFreq,
    double minGain,
    double maxGain)
{
    private readonly double _logFreqRange = Math.Log10(maxFreq / minFreq);

    public double Width => width;
    public double Height => height;

    public double FreqToX(double freq) =>
        width * (Math.Log10(freq / minFreq) / _logFreqRange);

    public double XToFreq(double x) =>
        minFreq * Math.Pow(maxFreq / minFreq, x / width);

    public double GainToY(double gain) =>
        height * (1.0 - (gain - minGain) / (maxGain - minGain));

    public double YToGain(double y) =>
        (1.0 - y / height) * (maxGain - minGain) + minGain;
}