using Equalizer.Services;
using System.Windows;

namespace Equalizer.Views;

public partial class EqualizerSettingsWindow : Window
{
    public EqualizerSettingsWindow()
    {
        InitializeComponent();
        ServiceLocator.WindowThemeService.Bind(this);
    }
}