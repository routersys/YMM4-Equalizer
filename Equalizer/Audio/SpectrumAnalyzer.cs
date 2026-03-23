namespace Equalizer.Audio;

public sealed class SpectrumAnalyzer
{
    public const int FftSize = 4096;
    public const int BinCount = FftSize / 2;
    private const float FloorDb = -120f;
    private const float AttackCoeff = 0.6f;
    private const float DecayCoeff = 0.08f;

    private readonly float[] _window = new float[FftSize];
    private readonly float[] _ringBuffer = new float[FftSize];
    private readonly float[] _fftReal = new float[FftSize];
    private readonly float[] _fftImag = new float[FftSize];
    private readonly float[] _rawDb = new float[BinCount];
    private readonly float[] _display = new float[BinCount];
    private readonly Lock _lock = new();
    private int _writePos;
    private long _totalPushed;
    private long _lastComputedPush;
    private long _version;

    public int SampleRate { get; set; }
    public bool HasData { get; private set; }
    public float[] DisplayMagnitudes => _display;
    public long Version => Volatile.Read(ref _version);

    public SpectrumAnalyzer()
    {
        for (int i = 0; i < FftSize; i++)
            _window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (FftSize - 1)));

        Array.Fill(_rawDb, FloorDb);
        Array.Fill(_display, FloorDb);
    }

    public void PushStereoInterleaved(float[] buffer, int offset, int frameCount)
    {
        lock (_lock)
        {
            for (int i = 0; i < frameCount; i++)
            {
                float mono = (buffer[offset + i * 2] + buffer[offset + i * 2 + 1]) * 0.5f;
                _ringBuffer[_writePos] = mono;
                _writePos = (_writePos + 1) & (FftSize - 1);
            }
            _totalPushed += frameCount;
        }
    }

    public bool TryCompute()
    {
        long pushed = Volatile.Read(ref _totalPushed);
        if (pushed < FftSize || pushed == _lastComputedPush) return false;

        lock (_lock)
        {
            pushed = _totalPushed;
            if (pushed == _lastComputedPush) return false;

            int pos = _writePos;
            for (int i = 0; i < FftSize; i++)
            {
                _fftReal[i] = _ringBuffer[pos] * _window[i];
                pos = (pos + 1) & (FftSize - 1);
            }
        }

        _lastComputedPush = pushed;

        Array.Clear(_fftImag, 0, FftSize);
        Fft(_fftReal, _fftImag, FftSize);

        for (int i = 0; i < BinCount; i++)
        {
            float re = _fftReal[i];
            float im = _fftImag[i];
            float mag = MathF.Sqrt(re * re + im * im) / FftSize;
            _rawDb[i] = 20f * MathF.Log10(MathF.Max(mag, 1e-10f));
        }

        HasData = true;
        Interlocked.Increment(ref _version);
        return true;
    }

    public bool Smooth()
    {
        if (!HasData) return false;

        bool changed = false;
        for (int i = 0; i < BinCount; i++)
        {
            float target = _rawDb[i];
            float current = _display[i];
            float coeff = target > current ? AttackCoeff : DecayCoeff;
            float next = current + coeff * (target - current);

            if (MathF.Abs(next - current) < 0.05f)
            {
                if (current != target)
                {
                    _display[i] = target;
                    changed = true;
                }
                continue;
            }

            _display[i] = next;
            changed = true;
        }

        if (changed)
            Interlocked.Increment(ref _version);

        return changed;
    }

    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_ringBuffer);
            _writePos = 0;
            _totalPushed = 0;
        }
        _lastComputedPush = 0;
        Array.Fill(_rawDb, FloorDb);
        Array.Fill(_display, FloorDb);
        HasData = false;
        Interlocked.Increment(ref _version);
    }

    private static void Fft(float[] real, float[] imag, int n)
    {
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            while ((j & bit) != 0)
            {
                j ^= bit;
                bit >>= 1;
            }
            j ^= bit;

            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            float angle = -2f * MathF.PI / len;
            float wRe = MathF.Cos(angle);
            float wIm = MathF.Sin(angle);

            for (int i = 0; i < n; i += len)
            {
                float curRe = 1f, curIm = 0f;
                int half = len >> 1;

                for (int j = 0; j < half; j++)
                {
                    int u = i + j;
                    int v = u + half;

                    float tRe = curRe * real[v] - curIm * imag[v];
                    float tIm = curRe * imag[v] + curIm * real[v];

                    real[v] = real[u] - tRe;
                    imag[v] = imag[u] - tIm;
                    real[u] += tRe;
                    imag[u] += tIm;

                    float next = curRe * wRe - curIm * wIm;
                    curIm = curRe * wIm + curIm * wRe;
                    curRe = next;
                }
            }
        }
    }
}