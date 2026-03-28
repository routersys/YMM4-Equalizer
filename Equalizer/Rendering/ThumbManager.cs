using Equalizer.Enums;
using Equalizer.Infrastructure;
using Equalizer.Localization;
using Equalizer.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Equalizer.Rendering;

internal sealed class ThumbManager(
    ThemePalette palette,
    Func<EditScope> createEditScope,
    Action<EQBand> onDelete,
    Action<Thumb, DragStartedEventArgs> onDragStarted,
    Action<Thumb, DragDeltaEventArgs> onDragDelta,
    Action<Thumb, DragCompletedEventArgs> onDragCompleted,
    Action<EQBand> onSelected)
{
    private static readonly FilterType[] AllFilterTypes = Enum.GetValues<FilterType>();
    private static readonly StereoMode[] AllStereoModes = Enum.GetValues<StereoMode>();

    private readonly Dictionary<EQBand, ThumbEntry> _cache = new(EqualizerAudioEffect.MaxBands);
    private readonly ControlTemplate?[] _templateCache = new ControlTemplate?[18];
    private ThemePalette _palette = palette;

    private const double ThumbSize = 12;
    private const double ThumbHalf = 6;

    public void ApplyPalette(ThemePalette palette)
    {
        _palette = palette;
        Array.Clear(_templateCache);
        foreach (var entry in _cache.Values)
            entry.InvalidateCache();
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
            FilterType type = band.Type;
            StereoMode mode = band.StereoMode;
            bool enabled = band.IsEnabled;

            bool stateDirty = type != entry.CachedType || mode != entry.CachedMode || enabled != entry.CachedEnabled;
            bool selectionDirty = isSelected != entry.CachedSelected;

            if (stateDirty || selectionDirty)
            {
                entry.Thumb.Template = ResolveTemplate(type == FilterType.Peak, enabled, isSelected, mode);
                entry.CachedSelected = isSelected;

                if (stateDirty)
                {
                    SyncContextMenu(entry, band);
                    UpdateTypeModeRun(entry, type, mode);
                    entry.CachedType = type;
                    entry.CachedMode = mode;
                    entry.CachedEnabled = enabled;
                }
            }

            double freq = band.Frequency.GetValue(currentFrame, totalFrames, 60);
            double gain = band.Gain.GetValue(currentFrame, totalFrames, 60);
            double q = band.Q.GetValue(currentFrame, totalFrames, 60);

            if (freq != entry.CachedFreq) { UpdateFreqRun(entry, freq); entry.CachedFreq = freq; }
            if (gain != entry.CachedGain) { UpdateGainRun(entry, gain); entry.CachedGain = gain; }
            if (q != entry.CachedQ) { UpdateQRun(entry, q); entry.CachedQ = q; }

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

    private ControlTemplate ResolveTemplate(bool isPeak, bool isEnabled, bool isSelected, StereoMode mode)
    {
        int fillIdx = !isEnabled ? 2 : isSelected ? 1 : 0;
        int modeIdx = mode == StereoMode.Left ? 1 : mode == StereoMode.Right ? 2 : 0;
        int idx = (isPeak ? 9 : 0) + (fillIdx * 3) + modeIdx;

        if (_templateCache[idx] is not null) return _templateCache[idx]!;

        Brush fill = fillIdx switch
        {
            1 => _palette.ThumbSelected,
            2 => Brushes.Gray,
            _ => _palette.ThumbFill
        };

        _templateCache[idx] = CreateTemplate(isPeak, fill, mode);
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
        tooltip.Inlines.Add(new Run(Texts.TooltipFrequencyPrefix) { FontWeight = FontWeights.Bold });
        tooltip.Inlines.Add(freqRun);
        tooltip.Inlines.Add(new Run(Texts.TooltipGainPrefix) { FontWeight = FontWeights.Bold });
        tooltip.Inlines.Add(gainRun);
        tooltip.Inlines.Add(new Run("  Q: ") { FontWeight = FontWeights.Bold });
        tooltip.Inlines.Add(qRun);

        var thumb = new Thumb { Width = ThumbSize, Height = ThumbSize, DataContext = band, Tag = band, ToolTip = tooltip };
        thumb.DragStarted += (s, e) => onDragStarted((Thumb)s!, e);
        thumb.DragDelta += (s, e) => onDragDelta((Thumb)s!, e);
        thumb.DragCompleted += (s, e) => onDragCompleted((Thumb)s!, e);
        thumb.PreviewMouseLeftButtonDown += (_, _) => onSelected(band);
        thumb.MouseDoubleClick += (_, e) => { onDelete(band); e.Handled = true; };

        var menu = BuildContextMenu(band);
        thumb.ContextMenu = menu;

        return new ThumbEntry(thumb, menu.Items[0] as MenuItem ?? new MenuItem(),
            CollectMenuItems(menu, 2), CollectMenuItems(menu, 3),
            typeModeRun, freqRun, gainRun, qRun);
    }

    private ContextMenu BuildContextMenu(EQBand band)
    {
        var menu = new ContextMenu();

        var enableItem = new MenuItem { Header = Texts.BandEnabled, IsCheckable = true };
        enableItem.Click += (_, _) =>
        {
            using var scope = createEditScope();
            band.IsEnabled = !band.IsEnabled;
        };
        menu.Items.Add(enableItem);
        menu.Items.Add(new Separator());

        var typeGroup = new MenuItem { Header = Texts.ContextFilterType };
        foreach (var type in AllFilterTypes)
        {
            var item = new MenuItem { Header = FilterName(type), IsCheckable = true };
            item.Click += (_, _) =>
            {
                if (band.Type == type) return;
                using var scope = createEditScope();
                band.Type = type;
            };
            typeGroup.Items.Add(item);
        }
        menu.Items.Add(typeGroup);

        var modeGroup = new MenuItem { Header = Texts.ContextStereoMode };
        foreach (var mode in AllStereoModes)
        {
            var item = new MenuItem { Header = ModeName(mode), IsCheckable = true };
            item.Click += (_, _) =>
            {
                if (band.StereoMode == mode) return;
                using var scope = createEditScope();
                band.StereoMode = mode;
            };
            modeGroup.Items.Add(item);
        }
        menu.Items.Add(modeGroup);

        menu.Items.Add(new Separator());
        var deleteItem = new MenuItem { Header = Texts.Delete };
        deleteItem.Click += (_, _) => onDelete(band);
        menu.Items.Add(deleteItem);

        return menu;
    }

    private static MenuItem[] CollectMenuItems(ContextMenu menu, int parentIndex)
    {
        if (parentIndex >= menu.Items.Count) return [];
        if (menu.Items[parentIndex] is not MenuItem parent) return [];
        return [.. parent.Items.OfType<MenuItem>()];
    }

    private static void SyncContextMenu(ThumbEntry entry, EQBand band)
    {
        entry.EnableItem.IsChecked = band.IsEnabled;
        for (int i = 0; i < entry.TypeItems.Length; i++)
            entry.TypeItems[i].IsChecked = band.Type == AllFilterTypes[i];
        for (int i = 0; i < entry.ModeItems.Length; i++)
            entry.ModeItems[i].IsChecked = band.StereoMode == AllStereoModes[i];
    }

    private static void UpdateFreqRun(ThumbEntry entry, double freq)
    {
        Span<char> buf = stackalloc char[24];
        freq.TryFormat(buf, out int len, "F0");
        " Hz".AsSpan().CopyTo(buf[len..]);
        entry.FreqRun.Text = new string(buf[..(len + 3)]);
    }

    private static void UpdateGainRun(ThumbEntry entry, double gain)
    {
        Span<char> buf = stackalloc char[24];
        gain.TryFormat(buf, out int len, "F1");
        " dB".AsSpan().CopyTo(buf[len..]);
        entry.GainRun.Text = new string(buf[..(len + 3)]);
    }

    private static void UpdateQRun(ThumbEntry entry, double q)
    {
        Span<char> buf = stackalloc char[16];
        q.TryFormat(buf, out int len, "F2");
        entry.QRun.Text = new string(buf[..len]);
    }

    private static void UpdateTypeModeRun(ThumbEntry entry, FilterType type, StereoMode mode)
    {
        var filterName = FilterName(type);
        var modeName = ModeName(mode);
        int totalLen = filterName.Length + 3 + modeName.Length;
        Span<char> buf = stackalloc char[64];
        filterName.AsSpan().CopyTo(buf);
        " | ".AsSpan().CopyTo(buf[filterName.Length..]);
        modeName.AsSpan().CopyTo(buf[(filterName.Length + 3)..]);
        entry.TypeModeRun.Text = new string(buf[..totalLen]);
    }

    private ControlTemplate CreateTemplate(bool isPeak, Brush fill, StereoMode mode)
    {
        var gridFactory = new FrameworkElementFactory(typeof(Grid));

        var shapeFactory = new FrameworkElementFactory(isPeak ? typeof(Ellipse) : typeof(Rectangle));
        shapeFactory.SetValue(Shape.FillProperty, fill);
        shapeFactory.SetValue(Shape.StrokeProperty, _palette.ThumbStroke);
        shapeFactory.SetValue(Shape.StrokeThicknessProperty, 1.5);
        gridFactory.AppendChild(shapeFactory);

        if (mode is StereoMode.Left or StereoMode.Right)
        {
            var canvasFactory = new FrameworkElementFactory(typeof(Canvas));
            canvasFactory.SetValue(UIElement.IsHitTestVisibleProperty, false);

            var pathFactory = new FrameworkElementFactory(typeof(Path));
            string pathData = mode == StereoMode.Left 
                ? "M-1,3 A3,3 0 0,0 -1,9 M-4,1 A5,5 0 0,0 -4,11 M-7,-1 A7,7 0 0,0 -7,13"
                : "M13,3 A3,3 0 0,1 13,9 M16,1 A5,5 0 0,1 16,11 M19,-1 A7,7 0 0,1 19,13";

            var brush = new SolidColorBrush(Color.FromRgb(255, 68, 68));
            brush.Freeze();

            pathFactory.SetValue(Path.DataProperty, Geometry.Parse(pathData));
            pathFactory.SetValue(Shape.StrokeProperty, brush);
            pathFactory.SetValue(Shape.StrokeThicknessProperty, 1.5);
            pathFactory.SetValue(Shape.StrokeStartLineCapProperty, PenLineCap.Round);
            pathFactory.SetValue(Shape.StrokeEndLineCapProperty, PenLineCap.Round);

            canvasFactory.AppendChild(pathFactory);
            gridFactory.AppendChild(canvasFactory);
        }

        return new ControlTemplate(typeof(Thumb)) { VisualTree = gridFactory };
    }

    private static string FilterName(FilterType type) => type switch
    {
        FilterType.Peak => Texts.FilterTypePeak,
        FilterType.LowShelf => Texts.FilterTypeLowShelf,
        FilterType.HighShelf => Texts.FilterTypeHighShelf,
        _ => type.ToString()
    };

    private static string ModeName(StereoMode mode) => mode switch
    {
        StereoMode.Stereo => Texts.StereoModeStereo,
        StereoMode.Left => Texts.StereoModeLeft,
        StereoMode.Right => Texts.StereoModeRight,
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

        public double CachedFreq = double.NaN;
        public double CachedGain = double.NaN;
        public double CachedQ = double.NaN;
        public FilterType CachedType = (FilterType)(-1);
        public StereoMode CachedMode = (StereoMode)(-1);
        public bool CachedEnabled;
        public bool CachedSelected;

        public void InvalidateCache()
        {
            CachedFreq = double.NaN;
            CachedGain = double.NaN;
            CachedQ = double.NaN;
            CachedType = (FilterType)(-1);
            CachedMode = (StereoMode)(-1);
        }
    }
}