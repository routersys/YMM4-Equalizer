using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using Equalizer.Enums;
using Equalizer.Localization;

namespace Equalizer.Models;

public class EQBand : Animatable
{
    private string _header = "";
    private bool _isEnabled;
    private bool _isUsed;
    private FilterType _type;
    private StereoMode _stereoMode;

    [Display(AutoGenerateField = true)]
    public string Header
    {
        get => _header;
        set => Set(ref _header, value);
    }

    [Display(Name = nameof(Texts.BandEnabled), ResourceType = typeof(Texts))]
    public bool IsEnabled
    {
        get => _isEnabled;
        set => Set(ref _isEnabled, value);
    }

    [Display(AutoGenerateField = false)]
    public bool IsUsed
    {
        get => _isUsed;
        set => Set(ref _isUsed, value);
    }

    [Display(Name = nameof(Texts.BandType), ResourceType = typeof(Texts))]
    public FilterType Type
    {
        get => _type;
        set
        {
            if (!Set(ref _type, value)) return;
            OnPropertyChanged(nameof(Gain));
        }
    }

    [Display(Name = nameof(Texts.BandChannel), ResourceType = typeof(Texts))]
    public StereoMode StereoMode
    {
        get => _stereoMode;
        set => Set(ref _stereoMode, value);
    }

    [Display(GroupName = nameof(Texts.BandSettingsGroup), Name = nameof(Texts.BandFrequency), ResourceType = typeof(Texts))]
    [AnimationSlider("F0", "Hz", 20, 20000)]
    public Animation Frequency { get; }

    [Display(GroupName = nameof(Texts.BandSettingsGroup), Name = nameof(Texts.BandGain), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "dB", -48, 48)]
    public Animation Gain { get; }

    [Display(GroupName = nameof(Texts.BandSettingsGroup), Name = nameof(Texts.BandQ), ResourceType = typeof(Texts))]
    [AnimationSlider("F2", "", 0.1, 18)]
    public Animation Q { get; }

    public EQBand() : this(true, FilterType.Peak, 1000, 0, 1, StereoMode.Stereo, Texts.BandDefaultName) { }

    public EQBand(bool enabled, FilterType type, double freq, double gain, double q, StereoMode mode, string header)
    {
        IsEnabled = enabled;
        Type = type;
        StereoMode = mode;
        Header = header;
        Frequency = new Animation(freq, 20, 20000);
        Gain = new Animation(gain, -48, 48);
        Q = new Animation(q, 0.1, 18);

        Frequency.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Frequency));
        Gain.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Gain));
        Q.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Q));
    }

    public void CopyFrom(EQBand other)
    {
        ArgumentNullException.ThrowIfNull(other);

        IsEnabled = other.IsEnabled;
        Type = other.Type;
        StereoMode = other.StereoMode;
        Header = other.Header;

        if (other.Frequency.Values.Count > 0)
            Frequency.Values[0].Value = other.Frequency.Values[0].Value;
        if (other.Gain.Values.Count > 0)
            Gain.Values[0].Value = other.Gain.Values[0].Value;
        if (other.Q.Values.Count > 0)
            Q.Values[0].Value = other.Q.Values[0].Value;
    }

    public BandSnapshot CreateSnapshot(long frame, long totalFrames, int hz) => new(
        IsEnabled,
        Type,
        StereoMode,
        (float)Frequency.GetValue(frame, totalFrames, hz),
        (float)Gain.GetValue(frame, totalFrames, hz),
        (float)Q.GetValue(frame, totalFrames, hz));

    protected override IEnumerable<IAnimatable> GetAnimatables() => [Frequency, Gain, Q];
}