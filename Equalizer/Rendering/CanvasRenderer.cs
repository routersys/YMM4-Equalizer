using Equalizer.Audio;
using Equalizer.Enums;
using Equalizer.Models;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Equalizer.Rendering;

internal sealed class CanvasRenderer
{
    private static readonly int[] FreqLabels = [50, 100, 200, 500, 1000, 2000, 5000, 10000];
    private static readonly Typeface GridTypeface = new("Segoe UI");
    private const double SplineTension = 0.175;
    private const double Dpi = 96.0;
    private const float SpectrumFloorDb = -80f;
    private const float SpectrumCeilingDb = 0f;

    private readonly StreamGeometry _curveGeometry = new();
    private readonly StreamGeometry _fillGeometry = new();
    private readonly StreamGeometry _spectrumGeometry = new();
    private readonly EQBand[] _sortBuffer = new EQBand[EqualizerAudioEffect.MaxBands];

    private ThemePalette? _cachedPalette;
    private Pen? _gridPen;
    private Pen? _zeroGridPen;
    private Pen? _curvePen;
    private Pen? _timelinePen;

    private void EnsurePens(ThemePalette palette)
    {
        if (ReferenceEquals(_cachedPalette, palette)) return;
        _cachedPalette = palette;
        _gridPen = FrozenPen(palette.GridLine, 1);
        _zeroGridPen = FrozenPen(palette.GridLine, 1.5);
        _curvePen = FrozenPen(palette.Curve, 2);
        _timelinePen = FrozenPen(palette.Timeline, 1);
    }

    public void RenderGrid(DrawingContext dc, CoordinateMapper mapper, ThemePalette palette)
    {
        EnsurePens(palette);

        foreach (int freq in FreqLabels)
        {
            double x = mapper.FreqToX(freq);
            dc.DrawLine(_gridPen!, new Point(x, 0), new Point(x, mapper.Height));

            string label = freq < 1000 ? $"{freq}" : $"{freq / 1000}k";
            var text = MakeText(label, 9, palette.GridText);
            dc.DrawText(text, new Point(x + 2, -2));
        }

        double maxGain = mapper.YToGain(0);
        int numLines = (int)(maxGain / 6);
        for (int i = -numLines; i <= numLines; i++)
        {
            if (i == 0) continue;
            double gain = i * 6;
            if (Math.Abs(gain) > maxGain) continue;

            double y = mapper.GainToY(gain);
            dc.DrawLine(_gridPen!, new Point(0, y), new Point(mapper.Width, y));

            var text = MakeText($"{gain:F0}", 9, palette.GridText);
            dc.DrawText(text, new Point(2, y));
        }

        double zeroY = mapper.GainToY(0);
        dc.DrawLine(_zeroGridPen!, new Point(0, zeroY), new Point(mapper.Width, zeroY));

        var zeroText = MakeText("0dB", 9, palette.GridText);
        dc.DrawText(zeroText, new Point(2, zeroY - 12));
    }

    public void RenderCurve(
        DrawingContext dc,
        CoordinateMapper mapper,
        ThemePalette palette,
        ObservableCollection<EQBand>? bands,
        int currentFrame,
        int totalFrames)
    {
        EnsurePens(palette);

        double zeroY = mapper.GainToY(0);
        double bottomY = mapper.Height;

        int activeCount = 0;
        if (bands is not null)
        {
            foreach (var b in bands)
            {
                if (b.IsEnabled)
                    _sortBuffer[activeCount++] = b;
            }
        }
        SortByFreq(_sortBuffer, activeCount, currentFrame, totalFrames);

        if (activeCount == 0)
        {
            using (var ctx = _curveGeometry.Open())
            {
                ctx.BeginFigure(new Point(0, zeroY), false, false);
                ctx.LineTo(new Point(mapper.Width, zeroY), true, false);
            }
            using (var ctx = _fillGeometry.Open())
            {
                ctx.BeginFigure(new Point(0, zeroY), true, true);
                ctx.LineTo(new Point(mapper.Width, zeroY), false, false);
                ctx.LineTo(new Point(mapper.Width, bottomY), false, false);
                ctx.LineTo(new Point(0, bottomY), false, false);
            }
        }
        else
        {
            Span<Point> pts = stackalloc Point[activeCount + 2];

            double startY = _sortBuffer[0].Type == FilterType.LowShelf
                ? mapper.GainToY(_sortBuffer[0].Gain.GetValue(currentFrame, totalFrames, 60))
                : zeroY;
            pts[0] = new Point(0, startY);

            for (int i = 0; i < activeCount; i++)
            {
                var b = _sortBuffer[i];
                pts[i + 1] = new Point(
                    mapper.FreqToX(b.Frequency.GetValue(currentFrame, totalFrames, 60)),
                    mapper.GainToY(b.Gain.GetValue(currentFrame, totalFrames, 60)));
            }

            double endY = _sortBuffer[activeCount - 1].Type == FilterType.HighShelf
                ? mapper.GainToY(_sortBuffer[activeCount - 1].Gain.GetValue(currentFrame, totalFrames, 60))
                : zeroY;
            pts[activeCount + 1] = new Point(mapper.Width, endY);

            using (var ctx = _curveGeometry.Open())
                WriteSpline(ctx, pts, false, 0);
            using (var ctx = _fillGeometry.Open())
                WriteSpline(ctx, pts, true, bottomY);
        }

        dc.DrawGeometry(palette.CurveFill, null, _fillGeometry);
        dc.DrawGeometry(null, _curvePen, _curveGeometry);
    }

    public void RenderSpectrum(
        DrawingContext dc,
        CoordinateMapper mapper,
        ThemePalette palette,
        float[]? magnitudes,
        int sampleRate)
    {
        if (magnitudes is null || sampleRate <= 0) return;

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
                double y = h - normalized * h;

                ctx.LineTo(new Point(px, y), true, false);
            }

            ctx.LineTo(new Point(w, h), true, false);
        }

        dc.DrawGeometry(palette.SpectrumFill, null, _spectrumGeometry);
    }

    public void RenderTimeline(DrawingContext dc, CoordinateMapper mapper, ThemePalette palette, double currentTime)
    {
        EnsurePens(palette);
        double x = mapper.Width * currentTime;
        dc.DrawLine(_timelinePen!, new Point(x, 0), new Point(x, mapper.Height));
    }

    private static void WriteSpline(StreamGeometryContext ctx, Span<Point> pts, bool fill, double bottomY)
    {
        ctx.BeginFigure(pts[0], fill, false);
        int last = pts.Length - 1;

        for (int i = 0; i < last; i++)
        {
            var p0 = i == 0 ? pts[0] : pts[i - 1];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = i == last - 1 ? pts[last] : pts[i + 2];

            ctx.BezierTo(
                new Point(p1.X + (p2.X - p0.X) * SplineTension, p1.Y + (p2.Y - p0.Y) * SplineTension),
                new Point(p2.X - (p3.X - p1.X) * SplineTension, p2.Y - (p3.Y - p1.Y) * SplineTension),
                p2, true, false);
        }

        if (!fill) return;
        ctx.LineTo(new Point(pts[last].X, bottomY), false, false);
        ctx.LineTo(new Point(pts[0].X, bottomY), false, false);
    }

    private static void SortByFreq(EQBand[] arr, int count, int frame, int total)
    {
        for (int i = 1; i < count; i++)
        {
            var key = arr[i];
            double keyFreq = key.Frequency.GetValue(frame, total, 60);
            int j = i - 1;
            while (j >= 0 && arr[j].Frequency.GetValue(frame, total, 60) > keyFreq)
            {
                arr[j + 1] = arr[j];
                j--;
            }
            arr[j + 1] = key;
        }
    }

    private static FormattedText MakeText(string text, double size, Brush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, GridTypeface, size, brush, Dpi);

    private static Pen FrozenPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }
}