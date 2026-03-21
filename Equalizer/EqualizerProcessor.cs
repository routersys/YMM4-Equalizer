using System.Runtime.CompilerServices;
using Equalizer.Audio;
using Equalizer.Enums;
using Equalizer.Models;
using YukkuriMovieMaker.Player.Audio.Effects;

namespace Equalizer;

internal sealed class EqualizerProcessor : AudioEffectProcessorBase
{
    private readonly EqualizerAudioEffect _item;
    private readonly EqualizerAlgorithm _algorithm;
    private readonly bool _useHighQuality;
    private readonly AudioBufferPool _bufferPool = new();

    private IFilter[] _filtersL;
    private IFilter[] _filtersR;
    private readonly OverSampler _overSamplerL = new();
    private readonly OverSampler _overSamplerR = new();

    public override int Hz => Input?.Hz ?? 0;
    public override long Duration => Input?.Duration ?? 0;

    public EqualizerProcessor(EqualizerAudioEffect item, TimeSpan duration)
    {
        _item = item;
        _algorithm = EqualizerSettings.Default.Algorithm;
        _useHighQuality = EqualizerSettings.Default.HighQualityMode;

        int bandCount = item.Bands.Count;
        _filtersL = CreateFilterArray(bandCount);
        _filtersR = CreateFilterArray(bandCount);
    }

    protected override int read(float[] destBuffer, int offset, int count)
    {
        if (Input is null) return 0;

        int readCount = Input.Read(destBuffer, offset, count);
        if (readCount <= 0) return readCount;

        int frames = readCount / 2;
        long startFrame = Position / 2;
        long totalFrames = Duration / 2;
        int hz = Hz;

        if (totalFrames > 0)
            _item.CurrentProgress = (double)startFrame / totalFrames;

        var bands = _item.Bands;
        int bandCount = bands.Count;
        if (bandCount == 0) return readCount;

        EnsureFilterCapacity(bandCount);

        var snapshots = _bufferPool.RentSnapshot(bandCount);
        var bufL = _bufferPool.RentFloat(frames);
        var bufR = _bufferPool.RentFloat(frames);

        try
        {
            DeinterleaveChannels(destBuffer, offset, frames, bufL, bufR);

            for (int i = 0; i < frames; i++)
            {
                long currentFrame = startFrame + i;

                for (int j = 0; j < bandCount; j++)
                    snapshots[j] = bands[j].CreateSnapshot(currentFrame, totalFrames, hz);

                ProcessSample(ref bufL[i], snapshots, bandCount, _filtersL, _overSamplerL, 0);
                ProcessSample(ref bufR[i], snapshots, bandCount, _filtersR, _overSamplerR, 1);
            }

            InterleaveChannels(bufL, bufR, frames, destBuffer, offset);
        }
        finally
        {
            _bufferPool.ReturnSnapshot(snapshots);
            _bufferPool.ReturnFloat(bufL);
            _bufferPool.ReturnFloat(bufR);
        }

        return readCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessSample(
        ref float sample,
        BandSnapshot[] snapshots,
        int bandCount,
        IFilter[] filters,
        OverSampler overSampler,
        int channelIndex)
    {
        int filterHz = _useHighQuality ? Hz * 2 : Hz;

        if (_useHighQuality)
        {
            overSampler.Upsample(sample, out float up1, out float up2);

            for (int j = 0; j < bandCount; j++)
            {
                ref readonly var snap = ref snapshots[j];
                if (!snap.IsEnabled || !ShouldProcess(snap.StereoMode, channelIndex)) continue;

                filters[j].SetCoefficients(snap.Type, filterHz, snap.Frequency, snap.Gain, snap.Q);
                up1 = filters[j].Process(up1);
                up2 = filters[j].Process(up2);
            }

            sample = overSampler.Downsample(up1, up2);
        }
        else
        {
            for (int j = 0; j < bandCount; j++)
            {
                ref readonly var snap = ref snapshots[j];
                if (!snap.IsEnabled || !ShouldProcess(snap.StereoMode, channelIndex)) continue;

                filters[j].SetCoefficients(snap.Type, filterHz, snap.Frequency, snap.Gain, snap.Q);
                sample = filters[j].Process(sample);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldProcess(StereoMode mode, int channelIndex) => mode switch
    {
        StereoMode.Stereo => true,
        StereoMode.Left => channelIndex == 0,
        StereoMode.Right => channelIndex == 1,
        _ => false
    };

    private static void DeinterleaveChannels(float[] source, int offset, int frames, float[] left, float[] right)
    {
        var src = source.AsSpan(offset, frames * 2);
        for (int i = 0; i < frames; i++)
        {
            left[i] = src[i * 2];
            right[i] = src[i * 2 + 1];
        }
    }

    private static void InterleaveChannels(float[] left, float[] right, int frames, float[] dest, int offset)
    {
        var dst = dest.AsSpan(offset, frames * 2);
        for (int i = 0; i < frames; i++)
        {
            dst[i * 2] = left[i];
            dst[i * 2 + 1] = right[i];
        }
    }

    private void EnsureFilterCapacity(int bandCount)
    {
        if (_filtersL.Length >= bandCount && _filtersR.Length >= bandCount) return;

        int oldCount = _filtersL.Length;
        Array.Resize(ref _filtersL, bandCount);
        Array.Resize(ref _filtersR, bandCount);

        for (int i = oldCount; i < bandCount; i++)
        {
            _filtersL[i] = FilterFactory.Create(_algorithm);
            _filtersR[i] = FilterFactory.Create(_algorithm);
        }
    }

    private IFilter[] CreateFilterArray(int count)
    {
        var filters = new IFilter[count];
        for (int i = 0; i < count; i++)
            filters[i] = FilterFactory.Create(_algorithm);
        return filters;
    }

    protected override void seek(long position)
    {
        Input?.Seek(position);
        foreach (var f in _filtersL) f?.Reset();
        foreach (var f in _filtersR) f?.Reset();
        _overSamplerL.Reset();
        _overSamplerR.Reset();
    }
}