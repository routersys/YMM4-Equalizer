using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Equalizer.Enums;
using Equalizer.Models;

namespace Equalizer.Rendering;

internal sealed class CanvasRenderer
{
    private const double SplineTension = 0.175;
    private static readonly int[] FreqLabels = [50, 100, 200, 500, 1000, 2000, 5000, 10000];

    private readonly StreamGeometry _curveGeometry = new();
    private readonly StreamGeometry _fillGeometry = new();
    private readonly Path _curvePath;
    private readonly Path _fillPath;
    private readonly EQBand[] _sortBuffer = new EQBand[EqualizerAudioEffect.MaxBands];

    private readonly List<UIElement> _gridCache = new(40);
    private double _gridW = -1, _gridH = -1, _gridMin = double.NaN, _gridMax = double.NaN;
    private object? _gridBrushToken;

    public CanvasRenderer(ThemePalette palette)
    {
        _curvePath = new Path { StrokeThickness = 2, Data = _curveGeometry, IsHitTestVisible = false };
        _fillPath = new Path { Data = _fillGeometry, IsHitTestVisible = false };
        Panel.SetZIndex(_fillPath, -2);
        Panel.SetZIndex(_curvePath, -1);
        ApplyPalette(palette);
    }

    public void ApplyPalette(ThemePalette palette)
    {
        _curvePath.Stroke = palette.Curve;
        _fillPath.Fill = palette.CurveFill;
        _gridBrushToken = null;
    }

    public void DrawGrid(Canvas canvas, CoordinateMapper mapper, ThemePalette palette)
    {
        bool dirty = _gridCache.Count == 0
            || _gridW != mapper.Width || _gridH != mapper.Height
            || _gridMin != mapper.GainToY(0) || _gridMax != mapper.GainToY(6)
            || !ReferenceEquals(_gridBrushToken, palette.GridLine);

        if (dirty)
        {
            _gridCache.Clear();
            BuildGrid(mapper, palette);
            _gridW = mapper.Width;
            _gridH = mapper.Height;
            _gridMin = mapper.GainToY(0);
            _gridMax = mapper.GainToY(6);
            _gridBrushToken = palette.GridLine;
        }

        foreach (var el in _gridCache)
            canvas.Children.Add(el);
    }

    public void DrawCurve(
        Canvas canvas,
        CoordinateMapper mapper,
        ObservableCollection<EQBand>? bands,
        int currentFrame,
        int totalFrames)
    {
        double zeroY = mapper.GainToY(0);
        double bottomY = mapper.Height;
        double width = mapper.Width;

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
                ctx.LineTo(new Point(width, zeroY), true, false);
            }
            using (var ctx = _fillGeometry.Open())
            {
                ctx.BeginFigure(new Point(0, zeroY), true, true);
                ctx.LineTo(new Point(width, zeroY), false, false);
                ctx.LineTo(new Point(width, bottomY), false, false);
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
            pts[activeCount + 1] = new Point(width, endY);

            using (var ctx = _curveGeometry.Open())
                WriteSpline(ctx, pts, false, 0);
            using (var ctx = _fillGeometry.Open())
                WriteSpline(ctx, pts, true, bottomY);
        }

        canvas.Children.Add(_fillPath);
        canvas.Children.Add(_curvePath);
    }

    public void DrawTimeline(Canvas canvas, double currentTime, ThemePalette palette)
    {
        double x = canvas.ActualWidth * currentTime;
        var line = new Line
        {
            X1 = x, Y1 = 0, X2 = x, Y2 = canvas.ActualHeight,
            Stroke = palette.Timeline, StrokeThickness = 1, IsHitTestVisible = false
        };
        Panel.SetZIndex(line, 100);
        canvas.Children.Add(line);
    }

    private void BuildGrid(CoordinateMapper mapper, ThemePalette palette)
    {
        foreach (int freq in FreqLabels)
        {
            double x = mapper.FreqToX(freq);
            _gridCache.Add(new Line
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = mapper.Height,
                Stroke = palette.GridLine, StrokeThickness = 1
            });
            _gridCache.Add(new System.Windows.Controls.TextBlock
            {
                Text = freq < 1000 ? $"{freq}" : $"{freq / 1000}k",
                Foreground = palette.GridText, FontSize = 9,
                Margin = new Thickness(x + 2, -2, 0, 0)
            });
        }

        double maxGain = mapper.YToGain(0);
        int numLines = (int)(maxGain / 6);
        for (int i = -numLines; i <= numLines; i++)
        {
            if (i == 0) continue;
            double gain = i * 6;
            if (Math.Abs(gain) > maxGain) continue;
            double y = mapper.GainToY(gain);
            _gridCache.Add(new Line
            {
                X1 = 0, Y1 = y, X2 = mapper.Width, Y2 = y,
                Stroke = palette.GridLine, StrokeThickness = 1
            });
            _gridCache.Add(new System.Windows.Controls.TextBlock
            {
                Text = $"{gain:F0}", Foreground = palette.GridText,
                FontSize = 9, Margin = new Thickness(2, y, 0, 0)
            });
        }

        double zeroY = mapper.GainToY(0);
        _gridCache.Add(new Line
        {
            X1 = 0, Y1 = zeroY, X2 = mapper.Width, Y2 = zeroY,
            Stroke = palette.GridLine, StrokeThickness = 1.5
        });
        _gridCache.Add(new System.Windows.Controls.TextBlock
        {
            Text = "0dB", Foreground = palette.GridText,
            FontSize = 9, Margin = new Thickness(2, zeroY - 12, 0, 0)
        });
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
}