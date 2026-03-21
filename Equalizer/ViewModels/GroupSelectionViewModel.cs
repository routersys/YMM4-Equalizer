using Equalizer.Models;
using Equalizer.Services;
using System.Collections.ObjectModel;

namespace Equalizer.ViewModels;

public sealed class GroupSelectionViewModel : ViewModelBase
{
    private GroupItem? _selectedGroup;

    public ObservableCollection<GroupItem> GroupOptions { get; } = [];

    public GroupItem? SelectedGroup
    {
        get => _selectedGroup;
        set => SetProperty(ref _selectedGroup, value);
    }

    public GroupSelectionViewModel(string currentGroup)
    {
        var groupService = ServiceLocator.GroupService;

        foreach (var g in groupService.UserGroups)
        {
            if (g.Tag is not ("favorites" or ""))
                GroupOptions.Add(g);
        }

        SelectedGroup =
            GroupOptions.FirstOrDefault(g => g.Tag == currentGroup) ??
            GroupOptions.FirstOrDefault(g => g.Tag == "other") ??
            GroupOptions.FirstOrDefault();
    }
}