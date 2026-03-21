using YukkuriMovieMaker.Plugin;
using Equalizer.Views;
using Equalizer.Interfaces;
using Equalizer.Services;

namespace Equalizer;

public enum EqualizerAlgorithm
{
    Biquad,
    TPT_SVF
}

public class EqualizerSettings : SettingsBase<EqualizerSettings>
{
    private readonly IConfigService _configService;

    public override string Name => "GUIイコライザー設定";
    public override SettingsCategory Category => SettingsCategory.AudioEffect;
    public override bool HasSettingView => false;
    public override object SettingView => new EqualizerSettingsWindow
    {
        DataContext = new ViewModels.EqualizerSettingsViewModel()
    };

    public EqualizerSettings()
    {
        _configService = ServiceLocator.ConfigService;
    }

    public bool HighQualityMode
    {
        get => _configService.HighQualityMode;
        set => _configService.HighQualityMode = value;
    }

    public double EditorHeight
    {
        get => _configService.EditorHeight;
        set => _configService.EditorHeight = value;
    }

    public string DefaultPreset
    {
        get => _configService.DefaultPreset;
        set => _configService.DefaultPreset = value;
    }

    public EqualizerAlgorithm Algorithm
    {
        get => _configService.Algorithm;
        set => _configService.Algorithm = value;
    }

    public override void Initialize()
    {
        _configService.Load();
    }

    public override void Save()
    {
        _configService.Save();
    }
}