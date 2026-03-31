using Equalizer.Infrastructure;
using Equalizer.Localization;
using Equalizer.Models;
using Equalizer.Rendering;
using Equalizer.Services;
using Equalizer.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;

namespace Equalizer.Views;

public partial class EqualizerControl : UserControl, IPropertyEditorControl
{
    private const double MinFreq = 20;
    private const double MaxFreq = 20000;
    private const double MinEditorHeight = 150;
    private const double MaxEditorHeight = 600;

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<EQBand>),
            typeof(EqualizerControl),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public ObservableCollection<EQBand> ItemsSource
    {
        get => (ObservableCollection<EQBand>)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private EqualizerAudioEffect? _effect;
    public new EqualizerAudioEffect? Effect
    {
        get => _effect;
        set
        {
            if (ReferenceEquals(_effect, value)) return;

            if (_isEventsAttached && _effect is INotifyPropertyChanged oldNotifier)
                oldNotifier.PropertyChanged -= OnEffectPropertyChanged;

            _effect = value;

            if (_effect is INotifyPropertyChanged newNotifier)
            {
                if (_isEventsAttached)
                    newNotifier.PropertyChanged += OnEffectPropertyChanged;

                if (ViewModel is not null)
                {
                    ViewModel.CurrentTime = _effect.CurrentProgress;
                    ViewModel.Effect = _effect;
                }
            }
        }
    }

    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    private EqualizerEditorViewModel ViewModel => (EqualizerEditorViewModel)DataContext;

    private ThemePalette _palette;
    private readonly ThumbManager _thumbManager;
    private readonly BandDragHandler _dragHandler = new();
    private readonly CompositeDisposable _bandSubscriptions = new();

    private bool _needsFullRedraw;
    private bool _isEditing;
    private bool _suppressTimeUpdate;
    private long _lastRenderedSpectrumVersion;
    private bool _isCompactMode;
    private bool _isUserDraggingSlider;
    private bool _isEventsAttached;

    static EqualizerControl()
    {
        ServiceLocator.RegisterToastPresenter(new WpfToastPresenter());
    }

    public EqualizerControl()
    {
        InitializeComponent();
        DataContext = new EqualizerEditorViewModel();

        _palette = ThemePalette.Detect(Colors.Black);
        _thumbManager = new ThumbManager(
            _palette,
            ViewModel.CreateEditScope,
            band => ViewModel.DeletePointCommand.Execute(band),
            OnThumbDragStarted,
            OnThumbDragDelta,
            OnThumbDragCompleted,
            band => ViewModel.SelectedBand = band);

        ViewModel.RequestRedraw += (_, _) =>
        {
            if (!_suppressTimeUpdate)
                _needsFullRedraw = true;
        };
        ViewModel.BeginEdit += (_, _) => BeginEdit?.Invoke(this, EventArgs.Empty);
        ViewModel.EndEdit += (_, _) => EndEdit?.Invoke(this, EventArgs.Empty);

        PresetToggleButton.Unchecked += async (_, _) =>
        {
            PresetToggleButton.IsHitTestVisible = false;
            await Task.Delay(200);
            PresetToggleButton.IsHitTestVisible = true;
        };

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private CoordinateMapper CreateMapper() =>
        new(VisualHost.ActualWidth, VisualHost.ActualHeight, MinFreq, MaxFreq, -ViewModel.Zoom, ViewModel.Zoom);

    private (int CurrentFrame, int TotalFrames) ComputeFrameInfo()
    {
        int maxFrames = 1;
        if (ItemsSource is { Count: > 0 })
        {
            for (int i = 0; i < ItemsSource.Count; i++)
            {
                var b = ItemsSource[i];
                int count = Math.Max(b.Frequency.Values.Count, Math.Max(b.Gain.Values.Count, b.Q.Values.Count));
                if (count > maxFrames) maxFrames = count;
            }
        }
        int totalFrames = Math.Max(maxFrames, 1000);
        int currentFrame = (int)(totalFrames * ViewModel.CurrentTime);
        return (currentFrame, totalFrames);
    }

    private void OnEffectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
    }

    private void OnRenderFrame(object? sender, EventArgs e)
    {
        if (!IsLoaded || !IsVisible) return;
        if (VisualHost.ActualWidth <= 0 || VisualHost.ActualHeight <= 0) return;

        if (_needsFullRedraw)
        {
            _needsFullRedraw = false;
            DrawAll();
        }

        if (_effect is null || ItemsSource is null) return;

        var spectrum = _effect.Spectrum;
        spectrum.TryCompute();

        bool hasNewAudioData = _effect.IsAudioDataDirty;
        _effect.IsAudioDataDirty = false;

        bool isPlaying = _effect.Clock.IsPlaying || hasNewAudioData;

        if (isPlaying && !_dragHandler.IsDragging && !_isUserDraggingSlider)
        {
            RenderPlaybackFrame(spectrum);
            return;
        }

        if (_isEditing)
        {
            RenderEditingFrame(spectrum);
            return;
        }

        if (hasNewAudioData)
        {
            _suppressTimeUpdate = true;
            ViewModel.CurrentTime = _effect.CurrentProgress;
            _suppressTimeUpdate = false;
            _needsFullRedraw = true;
        }

        RenderSpectrumSmoothing(spectrum);
    }

    private void RenderPlaybackFrame(Audio.SpectrumAnalyzer spectrum)
    {
        double progress = _effect!.Clock.GetInterpolatedProgress();

        _suppressTimeUpdate = true;
        ViewModel.CurrentTime = progress;
        _suppressTimeUpdate = false;

        var mapper = CreateMapper();
        var (currentFrame, totalFrames) = ComputeFrameInfo();

        bool spectrumChanged = spectrum.Smooth();
        long specVer = spectrum.Version;
        if (spectrumChanged && specVer != _lastRenderedSpectrumVersion)
        {
            _lastRenderedSpectrumVersion = specVer;
            VisualHost.RebuildSpectrum(mapper, _palette, spectrum.DisplayMagnitudes, spectrum.SampleRate);
        }

        VisualHost.RedrawCurveAndTimeline(mapper, _palette, ItemsSource, currentFrame, totalFrames, progress);
        _thumbManager.UpdatePositions(ItemsSource, mapper, currentFrame, totalFrames, false, null);
    }

    private void RenderEditingFrame(Audio.SpectrumAnalyzer spectrum)
    {
        var mapper = CreateMapper();
        var (currentFrame, totalFrames) = ComputeFrameInfo();

        bool spectrumChanged = spectrum.Smooth();
        long specVer = spectrum.Version;
        if (spectrumChanged && specVer != _lastRenderedSpectrumVersion)
        {
            _lastRenderedSpectrumVersion = specVer;
            VisualHost.RebuildSpectrum(mapper, _palette, spectrum.DisplayMagnitudes, spectrum.SampleRate);
        }

        VisualHost.RedrawCurveAndTimeline(mapper, _palette, ItemsSource, currentFrame, totalFrames, ViewModel.CurrentTime);
        _thumbManager.UpdatePositions(ItemsSource, mapper, currentFrame, totalFrames, _dragHandler.IsDragging, _dragHandler.DraggingBand);
    }

    private void RenderSpectrumSmoothing(Audio.SpectrumAnalyzer spectrum)
    {
        if (!spectrum.HasData) return;

        bool spectrumChanged = spectrum.Smooth();
        long specVer = spectrum.Version;
        if (!spectrumChanged || specVer == _lastRenderedSpectrumVersion) return;

        _lastRenderedSpectrumVersion = specVer;
        var mapper = CreateMapper();
        VisualHost.RebuildSpectrum(mapper, _palette, spectrum.DisplayMagnitudes, spectrum.SampleRate);
    }

    private void AttachEvents()
    {
        if (_isEventsAttached) return;
        _isEventsAttached = true;

        if (_effect is INotifyPropertyChanged effectNotifier)
            effectNotifier.PropertyChanged += OnEffectPropertyChanged;

        if (ItemsSource is not null)
        {
            ItemsSource.CollectionChanged += OnBandsCollectionChanged;
            SubscribeBands(ItemsSource);
        }
    }

    private void DetachEvents()
    {
        if (!_isEventsAttached) return;
        _isEventsAttached = false;

        if (_effect is INotifyPropertyChanged effectNotifier)
            effectNotifier.PropertyChanged -= OnEffectPropertyChanged;

        if (ItemsSource is not null)
            ItemsSource.CollectionChanged -= OnBandsCollectionChanged;

        _bandSubscriptions.Clear();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var dp = DependencyPropertyDescriptor.FromProperty(Border.BackgroundProperty, typeof(Border));
        dp?.AddValueChanged(CanvasBorder, OnBackgroundChanged);
        AttachEvents();
        UpdateTheme();
        _needsFullRedraw = true;
        CompositionTarget.Rendering += OnRenderFrame;

        if (_effect is null || ViewModel is null) return;
        ViewModel.CurrentTime = _effect.CurrentProgress;
        ViewModel.Effect = _effect;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var dp = DependencyPropertyDescriptor.FromProperty(Border.BackgroundProperty, typeof(Border));
        dp?.RemoveValueChanged(CanvasBorder, OnBackgroundChanged);
        DetachEvents();
        CompositionTarget.Rendering -= OnRenderFrame;
    }

    private void OnBackgroundChanged(object? sender, EventArgs e)
    {
        UpdateTheme();
        _needsFullRedraw = true;
    }

    private void UpdateTheme()
    {
        if (CanvasBorder.Background is not SolidColorBrush bg) return;
        _palette = ThemePalette.Detect(bg.Color);
        _thumbManager.ApplyPalette(_palette);
        VisualHost.InvalidateGrid();
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EqualizerControl)d;

        if (e.OldValue is ObservableCollection<EQBand> oldSource)
        {
            if (control._isEventsAttached)
            {
                oldSource.CollectionChanged -= control.OnBandsCollectionChanged;
                control._bandSubscriptions.Clear();
            }
            control._thumbManager.ClearCache();
        }

        control.ViewModel.Bands = e.NewValue as ObservableCollection<EQBand>;

        if (e.NewValue is ObservableCollection<EQBand> newSource && control._isEventsAttached)
        {
            newSource.CollectionChanged += control.OnBandsCollectionChanged;
            control.SubscribeBands(newSource);
        }

        control.UpdateDefaultSelection();
        control.UpdateTimeSliderRange();
        control._needsFullRedraw = true;
    }

    private void SubscribeBands(IEnumerable<EQBand> bands)
    {
        foreach (var band in bands)
            _bandSubscriptions.Subscribe(band, OnBandPropertyChanged);
    }

    private void OnBandsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<EQBand>())
                _thumbManager.Remove(item);
        }

        _bandSubscriptions.Clear();
        if (ItemsSource is not null && _isEventsAttached)
            SubscribeBands(ItemsSource);

        UpdateBandHeaders();
        UpdateDefaultSelection();
        UpdateTimeSliderRange();
        _needsFullRedraw = true;
    }

    private void OnBandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_dragHandler.IsDragging) return;

        if (e.PropertyName is nameof(EQBand.Frequency) or nameof(EQBand.Gain) or nameof(EQBand.Q))
            UpdateTimeSliderRange();

        _needsFullRedraw = true;
    }

    private void UpdateDefaultSelection()
    {
        if (ViewModel.SelectedBand is not null || ItemsSource is not { Count: > 0 }) return;

        ViewModel.SelectedBand = ItemsSource
            .OrderBy(b => b.Frequency.Values.FirstOrDefault()?.Value ?? 0)
            .FirstOrDefault();
    }

    private void UpdateTimeSliderRange()
    {
        if (ItemsSource is not { Count: > 0 }) return;

        int maxFrames = 1;
        for (int i = 0; i < ItemsSource.Count; i++)
        {
            var b = ItemsSource[i];
            int count = Math.Max(b.Frequency.Values.Count, Math.Max(b.Gain.Values.Count, b.Q.Values.Count));
            if (count > maxFrames) maxFrames = count;
        }

        TimeSlider.Maximum = 1.0;
        TimeSlider.TickFrequency = maxFrames > 1 ? 1.0 / (maxFrames - 1) : 0.1;
    }

    private void UpdateBandHeaders()
    {
        if (ItemsSource is null) return;
        var sorted = ItemsSource.OrderBy(b => b.Frequency.GetValue(0, 1, 60)).ToList();
        for (int i = 0; i < sorted.Count; i++)
            sorted[i].Header = string.Format(Texts.BandNameWithNumber, i + 1);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _needsFullRedraw = true;
    }

    private void DrawAll()
    {
        if (ItemsSource is null || VisualHost.ActualWidth <= 0 || VisualHost.ActualHeight <= 0) return;

        var mapper = CreateMapper();
        var (currentFrame, totalFrames) = ComputeFrameInfo();

        var spectrum = _effect?.Spectrum;
        bool hasSpectrum = spectrum is { HasData: true };

        if (hasSpectrum)
        {
            spectrum!.TryCompute();
            spectrum.Smooth();
            _lastRenderedSpectrumVersion = spectrum.Version;
        }

        VisualHost.Redraw(mapper, _palette, ItemsSource,
            currentFrame, totalFrames, ViewModel.CurrentTime,
            hasSpectrum ? spectrum!.DisplayMagnitudes : null,
            spectrum?.SampleRate ?? 0);

        ThumbCanvas.Children.Clear();
        _thumbManager.DrawThumbs(ThumbCanvas, ItemsSource, mapper,
            currentFrame, totalFrames,
            ViewModel.SelectedBand, _dragHandler.IsDragging, _dragHandler.DraggingBand);
    }

    private void OnThumbDragStarted(Thumb thumb, DragStartedEventArgs e)
    {
        if (thumb.DataContext is not EQBand band) return;

        ViewModel.SelectedBand = band;
        var (_, totalFrames) = ComputeFrameInfo();
        _dragHandler.Start(thumb, band, CreateMapper(), ViewModel.CurrentTime, totalFrames);
        BeginEdit?.Invoke(this, EventArgs.Empty);
    }

    private void OnThumbDragDelta(Thumb thumb, DragDeltaEventArgs e)
    {
        if (thumb.DataContext is not EQBand band || !band.IsEnabled) return;

        var mapper = CreateMapper();
        _dragHandler.Update(thumb, e, mapper, VisualHost.ActualWidth, VisualHost.ActualHeight);

        var (currentFrame, totalFrames) = ComputeFrameInfo();
        VisualHost.RedrawCurveAndTimeline(mapper, _palette, ItemsSource,
            currentFrame, totalFrames, ViewModel.CurrentTime);
    }

    private void OnThumbDragCompleted(Thumb thumb, DragCompletedEventArgs e)
    {
        _dragHandler.Complete();
        _needsFullRedraw = true;
        EndEdit?.Invoke(this, EventArgs.Empty);
    }

    private void ThumbCanvas_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var pos = Mouse.GetPosition(ThumbCanvas);
        var mapper = CreateMapper();
        ThumbCanvas.Tag = e.Source is FrameworkElement { DataContext: EQBand band }
            ? band
            : new Point(mapper.XToFreq(pos.X), mapper.YToGain(pos.Y));
    }

    private void ThumbCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) return;
        if (!ReferenceEquals(e.OriginalSource, ThumbCanvas)) return;

        var pos = e.GetPosition(ThumbCanvas);
        var mapper = CreateMapper();
        ViewModel.AddPointCommand.Execute(new Point(mapper.XToFreq(pos.X), mapper.YToGain(pos.Y)));
    }

    private void TimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || _dragHandler.IsDragging) return;

        _isUserDraggingSlider = TimeSlider.IsMouseCaptureWithin;
        _needsFullRedraw = true;

        if (!TimeSlider.IsMouseCaptureWithin)
            _isUserDraggingSlider = false;
    }

    private void HeaderGrid_SizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateHeaderLayout(e.NewSize.Width);

    private void UpdateHeaderLayout(double width)
    {
        bool shouldBeCompact = width < 420;
        if (shouldBeCompact == _isCompactMode) return;
        _isCompactMode = shouldBeCompact;

        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(200))
        {
            BeginTime = TimeSpan.FromMilliseconds(100)
        };

        AnimateTransition(ZoomPanel, fadeOut, fadeIn, () =>
        {
            if (_isCompactMode)
            {
                Grid.SetRow(ZoomPanel, 1); Grid.SetColumn(ZoomPanel, 0);
                ZoomPanel.HorizontalAlignment = HorizontalAlignment.Left;
                ZoomPanel.Margin = new Thickness(0, 5, 0, 0);
            }
            else
            {
                Grid.SetRow(ZoomPanel, 0); Grid.SetColumn(ZoomPanel, 2);
                ZoomPanel.HorizontalAlignment = HorizontalAlignment.Right;
                ZoomPanel.Margin = new Thickness(10, 0, 10, 0);
            }
        });

        AnimateTransition(SettingsButton, fadeOut, fadeIn, () =>
        {
            if (_isCompactMode)
            {
                Grid.SetRow(SettingsButton, 1); Grid.SetColumn(SettingsButton, 1);
                SettingsButton.HorizontalAlignment = HorizontalAlignment.Left;
                SettingsButton.Margin = new Thickness(5, 5, 0, 0);
            }
            else
            {
                Grid.SetRow(SettingsButton, 0); Grid.SetColumn(SettingsButton, 3);
                SettingsButton.HorizontalAlignment = HorizontalAlignment.Right;
                SettingsButton.Margin = new Thickness(0);
            }
        });
    }

    private static void AnimateTransition(
        UIElement element,
        System.Windows.Media.Animation.DoubleAnimation fadeOut,
        System.Windows.Media.Animation.DoubleAnimation fadeIn,
        Action layoutChange)
    {
        var localFadeOut = fadeOut.Clone();
        localFadeOut.Completed += (_, _) =>
        {
            layoutChange();
            element.BeginAnimation(OpacityProperty, fadeIn);
        };
        element.BeginAnimation(OpacityProperty, localFadeOut);
    }

    private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e) =>
        ViewModel.NotifyBeginEdit();

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e) =>
        ViewModel.EditorHeight = Math.Clamp(EditorGrid.ActualHeight + e.VerticalChange, MinEditorHeight, MaxEditorHeight);

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e) =>
        ViewModel.NotifyEndEdit();

    private void Band_BeginEdit(object? sender, EventArgs e)
    {
        ViewModel.NotifyBeginEdit();
        _isEditing = true;
    }

    private void Band_EndEdit(object? sender, EventArgs e)
    {
        ViewModel.NotifyEndEdit();
        _isEditing = false;
        _needsFullRedraw = true;
    }

    private void PresetToggleButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (PresetToggleButton.IsChecked != true) return;
        ViewModel.IsPopupOpen = false;
        e.Handled = true;
    }

    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender
        };
        RaiseEvent(eventArg);
    }
}