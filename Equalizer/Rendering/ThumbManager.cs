using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Equalizer.Enums;
using Equalizer.Infrastructure;
using Equalizer.Models;

namespace Equalizer.Rendering;

internal sealed class ThumbManager
{
    private const double ThumbSize = 12;
    private const double ThumbHalf = 6;

    private static readonly FilterType[] AllFilterTypes = Enum.GetValues<FilterType>();
    private static readonly StereoMode[] AllStereoModes = Enum.GetValues<StereoMode>();

    private readonly Dictionary<EQBand, ThumbEntry> _cache = new(EqualizerAudioEffect.MaxBands);
    private readonly ControlTemplate?[] _templateCache = new ControlTemplate?[6];

    private readonly Func<EditScope> _createEditScope;
    private readonly Action<EQBand> _onDelete;
    private readonly Action<Thumb, DragStartedEventArgs> _onDragStarted;
    private readonly Action<Thumb, DragDeltaEventArgs> _onDragDelta;
    private readonly Action<Thumb, DragCompletedEventArgs> _onDragCompleted;
    private readonly Action<EQBand> _onSelected;

    private ThemePalette _palette;

    public ThumbManager(
        ThemePalette palette,
        Func<EditScope> createEditScope,
        Action<EQBand> onDelete,
        Action<Thumb, DragStartedEventArgs> onDragStarted,
        Action<Thumb, DragDeltaEventArgs> onDragDelta,
        Action<Thumb, DragCompletedEventArgs> onDragCompleted,
        Action<EQBand> onSelected)
    {
        _palette = palette;
        _createEditScope = createEditScope;
        _onDelete = onDelete;
        _onDragStarted = onDragStarted;
        _onDragDelta = onDragDelta;
        _onDragCompleted = onDragCompleted;
        _onSelected = onSelected;
    }

    public void ApplyPalette(ThemePalette palette)
    {
        _palette = palette;
        Array.Clear(_templateCache);
    }

    public void DrawThumbs(
        Canvas canvas,
        ObservableCollection<EQBand>? bands,
        CoordinateMapper mapper,
        int currentFrame,
        int totalFrames,
        EQBand? selectedBand,
        bool isDragging,
        EQBand? draggingBand)
    {
        if (bands is null) return;

        for (int i = 0; i < bands.Count; i++)
        {
            var band = bands[i];

            if (!_cache.TryGetValue(band, out var entry))
            {
                entry = CreateEntry(band);
                _cache[band] = entry;
            }

            bool isSelected = ReferenceEquals(band, selectedBand);
            entry.Thumb.Template = ResolveTemplate(band.Type == FilterType.Peak, band.IsEnabled, isSelected);
            SyncContextMenu(entry, band);

            double freq = band.Frequency.GetValue(currentFrame, totalFrames, 60);
            double gain = band.Gain.GetValue(currentFrame, totalFrames, 60);
            double q = band.Q.GetValue(currentFrame, totalFrames, 60);

            entry.TypeModeRun.Text = $"{FilterName(band.Type)} | {ModeName(band.StereoMode)}";
            entry.FreqRun.Text = $"{freq:F0} Hz";
            entry.GainRun.Text = $"{gain:F1} dB";
            entry.QRun.Text = $"{q:F2}";

            if (!isDragging || !ReferenceEquals(band, draggingBand))
            {
                Canvas.SetLeft(entry.Thumb, mapper.FreqToX(freq) - ThumbHalf);
                Canvas.SetTop(entry.Thumb, mapper.GainToY(gain) - ThumbHalf);
            }

            Panel.SetZIndex(entry.Thumb, 1);
            canvas.Children.Add(entry.Thumb);
        }
    }

    public void UpdatePositions(
        ObservableCollection<EQBand>? bands,
        CoordinateMapper mapper,
        int currentFrame,
        int totalFrames,
        bool isDragging,
        EQBand? draggingBand)
    {
        if (bands is null) return;

        for (int i = 0; i < bands.Count; i++)
        {
            var band = bands[i];
            if (!_cache.TryGetValue(band, out var entry)) continue;
            if (isDragging && ReferenceEquals(band, draggingBand)) continue;

            double freq = band.Frequency.GetValue(currentFrame, totalFrames, 60);
            double gain = band.Gain.GetValue(currentFrame, totalFrames, 60);

            Canvas.SetLeft(entry.Thumb, mapper.FreqToX(freq) - ThumbHalf);
            Canvas.SetTop(entry.Thumb, mapper.GainToY(gain) - ThumbHalf);
        }
    }

    public void Remove(EQBand band) => _cache.Remove(band);
    public void ClearCache() => _cache.Clear();

    private ControlTemplate ResolveTemplate(bool isPeak, bool isEnabled, bool isSelected)
    {
        int fillIdx = !isEnabled ? 2 : isSelected ? 1 : 0;
        int idx = (isPeak ? 0 : 3) + fillIdx;

        if (_templateCache[idx] is not null) return _templateCache[idx]!;

        Brush fill = fillIdx switch
        {
            1 => _palette.ThumbSelected,
            2 => Brushes.Gray,
            _ => _palette.ThumbFill
        };

        _templateCache[idx] = isPeak ? EllipseTemplate(fill) : RectangleTemplate(fill);
        return _templateCache[idx]!;
    }

    private ThumbEntry CreateEntry(EQBand band)
    {
        var typeModeRun = new Run();
        var freqRun = new Run();
        var gainRun = new Run();
        var qRun = new Run();

        var tooltip = new TextBlock();
        tooltip.Inlines.Add(typeModeRun);
        tooltip.Inlines.Add(new LineBreak());
        tooltip.Inlines.Add(new Run("周波数: ") { FontWeight = FontWeights.Bold });
        tooltip.Inlines.Add(freqRun);
        tooltip.Inlines.Add(new Run("  ゲイン: ") { FontWeight = FontWeights.Bold });
        tooltip.Inlines.Add(gainRun);
        tooltip.Inlines.Add(new Run("  Q: ") { FontWeight = FontWeights.Bold });
        tooltip.Inlines.Add(qRun);

        var thumb = new Thumb { Width = ThumbSize, Height = ThumbSize, DataContext = band, Tag = band, ToolTip = tooltip };
        thumb.DragStarted += (s, e) => _onDragStarted((Thumb)s!, e);
        thumb.DragDelta += (s, e) => _onDragDelta((Thumb)s!, e);
        thumb.DragCompleted += (s, e) => _onDragCompleted((Thumb)s!, e);
        thumb.PreviewMouseLeftButtonDown += (_, _) => _onSelected(band);
        thumb.MouseDoubleClick += (_, e) => { _onDelete(band); e.Handled = true; };

        var menu = BuildContextMenu(band);
        thumb.ContextMenu = menu;

        return new ThumbEntry(thumb, menu.Items[0] as MenuItem ?? new MenuItem(),
            CollectMenuItems(menu, 2), CollectMenuItems(menu, 3),
            typeModeRun, freqRun, gainRun, qRun);
    }

    private ContextMenu BuildContextMenu(EQBand band)
    {
        var menu = new ContextMenu();

        var enableItem = new MenuItem { Header = "有効", IsCheckable = true };
        enableItem.Click += (_, _) =>
        {
            using var scope = _createEditScope();
            band.IsEnabled = !band.IsEnabled;
        };
        menu.Items.Add(enableItem);
        menu.Items.Add(new Separator());

        var typeGroup = new MenuItem { Header = "フィルタの種類" };
        foreach (var type in AllFilterTypes)
        {
            var item = new MenuItem { Header = FilterName(type), IsCheckable = true };
            item.Click += (_, _) =>
            {
                if (band.Type == type) return;
                using var scope = _createEditScope();
                band.Type = type;
            };
            typeGroup.Items.Add(item);
        }
        menu.Items.Add(typeGroup);

        var modeGroup = new MenuItem { Header = "ステレオモード" };
        foreach (var mode in AllStereoModes)
        {
            var item = new MenuItem { Header = ModeName(mode), IsCheckable = true };
            item.Click += (_, _) =>
            {
                if (band.StereoMode == mode) return;
                using var scope = _createEditScope();
                band.StereoMode = mode;
            };
            modeGroup.Items.Add(item);
        }
        menu.Items.Add(modeGroup);

        menu.Items.Add(new Separator());
        var deleteItem = new MenuItem { Header = "削除" };
        deleteItem.Click += (_, _) => _onDelete(band);
        menu.Items.Add(deleteItem);

        return menu;
    }

    private static MenuItem[] CollectMenuItems(ContextMenu menu, int parentIndex)
    {
        if (parentIndex >= menu.Items.Count) return [];
        if (menu.Items[parentIndex] is not MenuItem parent) return [];
        return parent.Items.OfType<MenuItem>().ToArray();
    }

    private static void SyncContextMenu(ThumbEntry entry, EQBand band)
    {
        entry.EnableItem.IsChecked = band.IsEnabled;
        for (int i = 0; i < entry.TypeItems.Length; i++)
            entry.TypeItems[i].IsChecked = band.Type == AllFilterTypes[i];
        for (int i = 0; i < entry.ModeItems.Length; i++)
            entry.ModeItems[i].IsChecked = band.StereoMode == AllStereoModes[i];
    }

    private ControlTemplate EllipseTemplate(Brush fill)
    {
        var factory = new FrameworkElementFactory(typeof(Ellipse));
        factory.SetValue(Shape.FillProperty, fill);
        factory.SetValue(Shape.StrokeProperty, _palette.ThumbStroke);
        factory.SetValue(Shape.StrokeThicknessProperty, 1.5);
        return new ControlTemplate(typeof(Thumb)) { VisualTree = factory };
    }

    private ControlTemplate RectangleTemplate(Brush fill)
    {
        var factory = new FrameworkElementFactory(typeof(Rectangle));
        factory.SetValue(Shape.FillProperty, fill);
        factory.SetValue(Shape.StrokeProperty, _palette.ThumbStroke);
        factory.SetValue(Shape.StrokeThicknessProperty, 1.5);
        return new ControlTemplate(typeof(Thumb)) { VisualTree = factory };
    }

    private static string FilterName(FilterType type) => type switch
    {
        FilterType.Peak => "ピーク",
        FilterType.LowShelf => "ローシェルフ",
        FilterType.HighShelf => "ハイシェルフ",
        _ => type.ToString()
    };

    private static string ModeName(StereoMode mode) => mode switch
    {
        StereoMode.Stereo => "ステレオ",
        StereoMode.Left => "L (左)",
        StereoMode.Right => "R (右)",
        _ => mode.ToString()
    };

    internal sealed class ThumbEntry(
        Thumb thumb, MenuItem enableItem, MenuItem[] typeItems, MenuItem[] modeItems,
        Run typeModeRun, Run freqRun, Run gainRun, Run qRun)
    {
        public Thumb Thumb { get; } = thumb;
        public MenuItem EnableItem { get; } = enableItem;
        public MenuItem[] TypeItems { get; } = typeItems;
        public MenuItem[] ModeItems { get; } = modeItems;
        public Run TypeModeRun { get; } = typeModeRun;
        public Run FreqRun { get; } = freqRun;
        public Run GainRun { get; } = gainRun;
        public Run QRun { get; } = qRun;
    }
}