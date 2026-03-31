using Equalizer.Attributes;
using Equalizer.Audio;
using Equalizer.Enums;
using Equalizer.Localization;
using Equalizer.Models;
using Equalizer.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;

namespace Equalizer;

[PluginDetails(AuthorName = "routersys", ContentId = "nc470046")]

[AudioEffect(nameof(Texts.ExtendedEqualizerName), [AudioEffectCategories.Filter], [nameof(Texts.EqualizerDisplayName)], ResourceType = typeof(Texts), IsAviUtlSupported = false)]
public sealed class EqualizerAudioEffect : AudioEffectBase
{
    public const int MaxBands = 32;

    public override string Label => ServiceLocator.EffectTrackerService.GetLabel(this);

    private EQBand[] _items = new EQBand[MaxBands];
    private bool _isDeserialized;
    private bool _suspendChangeTracking;

    public EQBand[] Items
    {
        get => _items;
        set
        {
            _isDeserialized = true;

            UnsubscribeBandPropertyChanged(_items);

            var normalized = NormalizeBandArray(value);
            _items = normalized;
            EnsureAllBandsInitialized();
            SubscribeBandPropertyChanged(_items);
            UpdateBandsCollection();
            ServiceLocator.EffectTrackerService.UpdateModifiedState(this);
            OnPropertyChanged(nameof(Items));
        }
    }

    [JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public string SelectedPresetName => ServiceLocator.EffectTrackerService.GetSelectedPresetName(this);

    [JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [Display(GroupName = "Equalizer", Name = "")]
    [EqualizerEditor(PropertyEditorSize = PropertyEditorSize.FullWidth)]
    public ObservableCollection<EQBand> Bands { get; } = [];

    [JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public double CurrentProgress
    {
        get => _currentProgress;
        set => Set(ref _currentProgress, value);
    }
    private double _currentProgress;

    [JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public SpectrumAnalyzer Spectrum { get; } = new();

    [JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    public PlaybackClock Clock { get; } = new();

    public EqualizerAudioEffect()
    {
        EnsureAllBandsInitialized();
        SubscribeBandPropertyChanged(_items);
        Bands.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Bands));
        LoadDefaultPreset();
    }

    public void ApplyBands(IEnumerable<EQBand> sourceBands)
    {
        ArgumentNullException.ThrowIfNull(sourceBands);

        _suspendChangeTracking = true;
        ResetAllBands();

        int index = 0;
        foreach (var src in sourceBands)
        {
            if (index >= MaxBands) break;

            var target = _items[index];
            target.IsUsed = true;
            target.CopyFrom(src);
            index++;
        }

        _suspendChangeTracking = false;

        UpdateBandsCollection();
        ServiceLocator.EffectTrackerService.UpdateModifiedState(this);
    }

    public void SelectPreset(string presetName)
    {
        ServiceLocator.EffectTrackerService.SelectPreset(this, presetName);
        OnPropertyChanged(nameof(SelectedPresetName));
        OnPropertyChanged(nameof(Label));
    }

    public void ClearSelectedPreset()
    {
        ServiceLocator.EffectTrackerService.ClearPreset(this);
        OnPropertyChanged(nameof(SelectedPresetName));
        OnPropertyChanged(nameof(Label));
    }

    public void RenameSelectedPreset(string presetName)
    {
        ServiceLocator.EffectTrackerService.RenamePreset(this, presetName);
        OnPropertyChanged(nameof(SelectedPresetName));
        OnPropertyChanged(nameof(Label));
    }

    public void UpdateBandsCollection()
    {
        Bands.Clear();

        foreach (var item in _items)
        {
            if (item is { IsUsed: true })
                Bands.Add(item);
        }

        OnPropertyChanged(nameof(Bands));
    }

    public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration) =>
        new EqualizerProcessor(this, duration);

    protected override IEnumerable<IAnimatable> GetAnimatables() =>
        _items.Where(x => x is not null);

    public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    private void LoadDefaultPreset()
    {
        if (_isDeserialized) return;

        var defaultPreset = EqualizerSettings.Default.DefaultPreset;
        if (!string.IsNullOrEmpty(defaultPreset))
        {
            var loadedBands = ServiceLocator.PresetService.LoadPreset(defaultPreset);
            if (loadedBands is not null)
            {
                ApplyBands(loadedBands);
                SelectPreset(defaultPreset);
                return;
            }
        }

        ResetAllBands();

        var band = _items[0];
        band.IsUsed = true;
        band.IsEnabled = true;
        band.Type = FilterType.Peak;
        band.Frequency.Values[0].Value = 500;
        band.Gain.Values[0].Value = 0;
        band.Q.Values[0].Value = 1.0;

        UpdateBandsCollection();
        ClearSelectedPreset();
    }

    private void ResetAllBands()
    {
        foreach (var item in _items)
            item.IsUsed = false;
    }

    private void EnsureAllBandsInitialized()
    {
        for (int i = 0; i < _items.Length; i++)
        {
            _items[i] ??= new EQBand { IsUsed = false };

            if (string.IsNullOrWhiteSpace(_items[i].Header))
                _items[i].Header = string.Format(Texts.BandNameWithNumber, i + 1);
        }
    }

    private void SubscribeBandPropertyChanged(IEnumerable<EQBand> bands)
    {
        foreach (var band in bands)
            band.PropertyChanged += OnBandPropertyChanged;
    }

    private void UnsubscribeBandPropertyChanged(IEnumerable<EQBand?> bands)
    {
        foreach (var band in bands)
        {
            if (band is null) continue;
            band.PropertyChanged -= OnBandPropertyChanged;
        }
    }

    private void OnBandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suspendChangeTracking) return;

        if (e.PropertyName == nameof(EQBand.IsUsed))
            UpdateBandsCollection();

        ServiceLocator.EffectTrackerService.UpdateModifiedState(this);
        OnPropertyChanged(nameof(Label));
    }

    private static EQBand[] NormalizeBandArray(EQBand[]? value)
    {
        var result = new EQBand[MaxBands];

        if (value is { Length: > 0 })
        {
            int copyCount = Math.Min(value.Length, MaxBands);
            Array.Copy(value, result, copyCount);
        }

        for (int i = 0; i < MaxBands; i++)
            result[i] ??= new EQBand { IsUsed = false };

        return result;
    }
}