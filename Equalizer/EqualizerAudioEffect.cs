using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Equalizer.Attributes;
using Equalizer.Enums;
using Equalizer.Models;
using Equalizer.Services;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;

namespace Equalizer;

[AudioEffect("EQUALIZER", ["MIDI"], ["イコライザー"], IsAviUtlSupported = false)]
public sealed class EqualizerAudioEffect : AudioEffectBase
{
    public const int MaxBands = 32;

    public override string Label => "EQUALIZER";

    private EQBand[] _items = new EQBand[MaxBands];
    private bool _isDeserialized;

    public EQBand[] Items
    {
        get => _items;
        set
        {
            _isDeserialized = true;

            var normalized = NormalizeBandArray(value);
            _items = normalized;
            UpdateBandsCollection();
            OnPropertyChanged(nameof(Items));
        }
    }

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

    public EqualizerAudioEffect()
    {
        EnsureAllBandsInitialized();
        Bands.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Bands));
        LoadDefaultPreset();
    }

    public void ApplyBands(IEnumerable<EQBand> sourceBands)
    {
        ArgumentNullException.ThrowIfNull(sourceBands);

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

        UpdateBandsCollection();
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
    }

    private void ResetAllBands()
    {
        foreach (var item in _items)
            item.IsUsed = false;
    }

    private void EnsureAllBandsInitialized()
    {
        for (int i = 0; i < _items.Length; i++)
            _items[i] ??= new EQBand { IsUsed = false, Header = $"バンド {i + 1}" };
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