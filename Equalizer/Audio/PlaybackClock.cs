using System.Diagnostics;

namespace Equalizer.Audio;

public sealed class PlaybackClock
{
    private const double StopThresholdSeconds = 1.5;
    private const int StableCountRequired = 3;

    private long _samplePosition;
    private long _totalSamples;
    private int _sampleRate;
    private long _timestampTicks;
    private double _smoothRate;
    private int _stableCount;
    private double _lastBufferDuration;

    public bool IsPlaying
    {
        get
        {
            long ts = Volatile.Read(ref _timestampTicks);
            if (ts == 0) return false;
            double elapsed = (Stopwatch.GetTimestamp() - ts) / (double)Stopwatch.Frequency;
            return elapsed < StopThresholdSeconds;
        }
    }

    public void Update(long samplePosition, long totalSamples, int sampleRate)
    {
        long prevPos = Volatile.Read(ref _samplePosition);
        long prevTs = Volatile.Read(ref _timestampTicks);
        long now = Stopwatch.GetTimestamp();

        long delta = samplePosition - prevPos;
        bool isSeek = delta < 0 || (sampleRate > 0 && delta > sampleRate * 2);

        if (isSeek || prevTs == 0)
        {
            _stableCount = 0;
            _smoothRate = 0;
            _lastBufferDuration = 0;
            Volatile.Write(ref _sampleRate, sampleRate);
            Volatile.Write(ref _totalSamples, totalSamples);
            Volatile.Write(ref _samplePosition, samplePosition);
            Volatile.Write(ref _timestampTicks, now);
            return;
        }

        double dt = (now - prevTs) / (double)Stopwatch.Frequency;

        if (dt > 0.01 && delta > 0)
        {
            double measured = delta / dt;
            _lastBufferDuration = dt;

            if (_stableCount >= StableCountRequired)
            {
                _smoothRate = _smoothRate * 0.8 + measured * 0.2;
            }
            else
            {
                _stableCount++;
                if (_stableCount >= StableCountRequired)
                    _smoothRate = measured;
            }
        }

        Volatile.Write(ref _sampleRate, sampleRate);
        Volatile.Write(ref _totalSamples, totalSamples);
        Volatile.Write(ref _samplePosition, samplePosition);
        Volatile.Write(ref _timestampTicks, now);
    }

    public double GetInterpolatedProgress()
    {
        long ts = Volatile.Read(ref _timestampTicks);
        if (ts == 0) return 0;

        long pos = Volatile.Read(ref _samplePosition);
        long total = Volatile.Read(ref _totalSamples);

        if (total <= 0) return 0;

        double baseProgress = (double)pos / total;

        if (_stableCount < StableCountRequired || _smoothRate <= 0)
            return Math.Clamp(baseProgress, 0, 1);

        double elapsed = (Stopwatch.GetTimestamp() - ts) / (double)Stopwatch.Frequency;

        if (elapsed > StopThresholdSeconds)
            return Math.Clamp(baseProgress, 0, 1);

        double maxExtrapolation = _lastBufferDuration > 0
            ? _lastBufferDuration * 1.5
            : 1.0;

        double clampedElapsed = Math.Min(elapsed, maxExtrapolation);
        double extrapolated = pos + clampedElapsed * _smoothRate;

        return Math.Clamp(extrapolated / total, baseProgress, 1);
    }

    public void Reset()
    {
        Volatile.Write(ref _timestampTicks, 0L);
        Volatile.Write(ref _samplePosition, 0L);
        _stableCount = 0;
        _smoothRate = 0;
        _lastBufferDuration = 0;
    }
}