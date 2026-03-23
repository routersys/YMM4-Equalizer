using Equalizer.Audio;
using Equalizer.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace Equalizer.Rendering;

internal sealed class EqualizerVisualHost : FrameworkElement
{
    private const float SpectrumFloorDb = -80f;
    private const float SpectrumCeilingDb = 0f;

    private readonly VisualCollection _children;
    private readonly DrawingVisual _spectrumVisual = new();
    private readonly DrawingVisual _gridVisual = new();
    private readonly DrawingVisual _curveVisual = new();
    private readonly DrawingVisual _timelineVisual = new();
    private readonly TranslateTransform _timelineTransform = new();
    private readonly CanvasRenderer _renderer = new();
    private readonly StreamGeometry _spectrumGeometry = new();

    private Pen? _timelinePen;
    private double _timelineHeight;

    private double _lastGridW;
    private double _lastGridH;
    private double _lastGridMinGain;
    private double _lastGridMaxGain;
    private ThemePalette? _lastGridPalette;

    public EqualizerVisualHost()
    {
        _timelineVisual.Transform = _timelineTransform;

        _children = new VisualCollection(this)
        {
            _spectrumVisual,
            _gridVisual,
            _curveVisual,
            _timelineVisual
        };
    }

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    public void Redraw(
        CoordinateMapper mapper,
        ThemePalette palette,
        ObservableCollection<EQBand>? bands,
        int currentFrame,
        int totalFrames,
        double currentTime,
        float[]? spectrum,
        int sampleRate)
    {
        RedrawGrid(mapper, palette);
        if (spectrum is not null)
            RebuildSpectrum(mapper, palette, spectrum, sampleRate);
        RedrawCurve(mapper, palette, bands, currentFrame, totalFrames);
        EnsureTimeline(mapper, palette);
        _timelineTransform.X = mapper.Width * currentTime;
    }

    public void RedrawCurveAndTimeline(
        CoordinateMapper mapper,
        ThemePalette palette,
        ObservableCollection<EQBand>? bands,
        int currentFrame,
        int totalFrames,
        double currentTime)
    {
        RedrawCurve(mapper, palette, bands, currentFrame, totalFrames);
        _timelineTransform.X = mapper.Width * currentTime;
    }

    public void RebuildSpectrum(CoordinateMapper mapper, ThemePalette palette, float[] magnitudes, int sampleRate)
    {
        if (sampleRate <= 0) return;

        double w = mapper.Width;
        double h = mapper.Height;
        int step = Math.Max(2, (int)(w / 400));

        using (var ctx = _spectrumGeometry.Open())
        {
            ctx.BeginFigure(new Point(0, h), true, true);

            for (int px = 0; px <= (int)w; px += step)
            {
                double freq = mapper.XToFreq(px);
                float binF = (float)(freq * SpectrumAnalyzer.FftSize / sampleRate);
                int bin = Math.Clamp((int)binF, 0, SpectrumAnalyzer.BinCount - 2);
                float frac = binF - bin;

                float db = magnitudes[bin] * (1f - frac) + magnitudes[bin + 1] * frac;
                double normalized = Math.Clamp((db - SpectrumFloorDb) / (SpectrumCeilingDb - SpectrumFloorDb), 0, 1);

                ctx.LineTo(new Point(px, h - normalized * h), true, false);
            }

            ctx.LineTo(new Point(w, h), true, false);
        }

        using var dc = _spectrumVisual.RenderOpen();
        dc.DrawGeometry(palette.SpectrumFill, null, _spectrumGeometry);
    }

    public void MoveTimeline(double x)
    {
        _timelineTransform.X = x;
    }

    public void InvalidateGrid()
    {
        _lastGridPalette = null;
    }

    private void EnsureTimeline(CoordinateMapper mapper, ThemePalette palette)
    {
        if (_timelineHeight == mapper.Height && _timelinePen is not null) return;

        _timelineHeight = mapper.Height;
        _timelinePen = new Pen(palette.Timeline, 1);
        _timelinePen.Freeze();

        using var dc = _timelineVisual.RenderOpen();
        dc.DrawLine(_timelinePen, new Point(0, 0), new Point(0, mapper.Height));
    }

    private void RedrawGrid(CoordinateMapper mapper, ThemePalette palette)
    {
        bool dirty = _lastGridW != mapper.Width
            || _lastGridH != mapper.Height
            || _lastGridMinGain != mapper.YToGain(mapper.Height)
            || _lastGridMaxGain != mapper.YToGain(0)
            || !ReferenceEquals(_lastGridPalette, palette);

        if (!dirty) return;

        _lastGridW = mapper.Width;
        _lastGridH = mapper.Height;
        _lastGridMinGain = mapper.YToGain(mapper.Height);
        _lastGridMaxGain = mapper.YToGain(0);
        _lastGridPalette = palette;

        using var dc = _gridVisual.RenderOpen();
        _renderer.RenderGrid(dc, mapper, palette);
    }

    private void RedrawCurve(
        CoordinateMapper mapper,
        ThemePalette palette,
        ObservableCollection<EQBand>? bands,
        int currentFrame,
        int totalFrames)
    {
        using var dc = _curveVisual.RenderOpen();
        _renderer.RenderCurve(dc, mapper, palette, bands, currentFrame, totalFrames);
    }
}