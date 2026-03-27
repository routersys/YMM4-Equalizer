using Equalizer.Enums;
using Equalizer.Localization;
using Equalizer.ViewModels;
using Equalizer.Views;
using YukkuriMovieMaker.Plugin;

namespace Equalizer;

public sealed class EqualizerSettings : SettingsBase<EqualizerSettings>
{
    public override string Name => Texts.EqualizerSettingsName;
    public override SettingsCategory Category => SettingsCategory.AudioEffect;
    public override bool HasSettingView => false;

    public override object SettingView => new EqualizerSettingsWindow
    {
        DataContext = new EqualizerSettingsViewModel()
    };

    public override void Initialize() { }

    public bool HighQualityMode { get; set; }
    public double EditorHeight { get; set; } = 240;
    public string DefaultPreset { get; set; } = "";
    public EqualizerAlgorithm Algorithm { get; set; } = EqualizerAlgorithm.Biquad;
}