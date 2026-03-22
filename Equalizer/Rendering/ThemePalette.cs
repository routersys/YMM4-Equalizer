using System.Windows.Media;

namespace Equalizer.Rendering;

internal sealed class ThemePalette
{
    public SolidColorBrush GridLine { get; }
    public SolidColorBrush GridText { get; }
    public SolidColorBrush ThumbFill { get; }
    public SolidColorBrush ThumbSelected { get; }
    public SolidColorBrush ThumbStroke { get; }
    public SolidColorBrush Curve { get; }
    public SolidColorBrush CurveFill { get; }
    public SolidColorBrush Timeline { get; }
    public SolidColorBrush SpectrumFill { get; }

    private ThemePalette(
        Color gridLine, Color gridText,
        Color thumbFill, Color thumbStroke,
        Color curve, Color curveFill,
        Color spectrumFill)
    {
        GridLine = Freeze(gridLine);
        GridText = Freeze(gridText);
        ThumbFill = Freeze(thumbFill);
        ThumbSelected = Freeze(Color.FromRgb(255, 215, 0));
        ThumbStroke = Freeze(thumbStroke);
        Curve = Freeze(curve);
        CurveFill = Freeze(curveFill);
        Timeline = Freeze(Color.FromArgb(150, 255, 50, 50));
        SpectrumFill = Freeze(spectrumFill);
    }

    public static ThemePalette Detect(Color background)
    {
        bool isDark = (background.R * 0.299 + background.G * 0.587 + background.B * 0.114) / 255.0 < 0.5;
        return isDark ? CreateDark() : CreateLight();
    }

    private static ThemePalette CreateDark() => new(
        gridLine: Color.FromArgb(50, 255, 255, 255),
        gridText: Color.FromArgb(100, 255, 255, 255),
        thumbFill: Color.FromRgb(0, 180, 255),
        thumbStroke: Colors.White,
        curve: Color.FromRgb(0, 200, 255),
        curveFill: Color.FromArgb(30, 0, 200, 255),
        spectrumFill: Color.FromArgb(25, 100, 220, 255));

    private static ThemePalette CreateLight() => new(
        gridLine: Color.FromArgb(50, 0, 0, 0),
        gridText: Color.FromArgb(100, 0, 0, 0),
        thumbFill: Color.FromRgb(0, 120, 215),
        thumbStroke: Colors.Black,
        curve: Color.FromRgb(0, 100, 200),
        curveFill: Color.FromArgb(30, 0, 100, 200),
        spectrumFill: Color.FromArgb(20, 0, 100, 200));

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}