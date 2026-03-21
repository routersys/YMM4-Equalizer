using Equalizer.ViewModels;
using System.Windows;

namespace Equalizer.Views;

public partial class EqualizerSettingsWindow : Window
{
    public EqualizerSettingsWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}