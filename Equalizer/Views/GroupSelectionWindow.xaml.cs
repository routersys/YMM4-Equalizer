using Equalizer.Models;
using Equalizer.ViewModels;
using System.Windows;

namespace Equalizer.Views;

public partial class GroupSelectionWindow : Window
{
    public GroupItem? SelectedGroup => ((GroupSelectionViewModel)DataContext).SelectedGroup;

    public GroupSelectionWindow(string currentGroup)
    {
        InitializeComponent();
        DataContext = new GroupSelectionViewModel(currentGroup);
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (((GroupSelectionViewModel)DataContext).SelectedGroup != null)
        {
            DialogResult = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}