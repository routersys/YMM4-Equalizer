using Equalizer.Models;
using System.Collections.ObjectModel;

namespace Equalizer.Interfaces;

public interface IGroupService
{
    ObservableCollection<GroupItem> UserGroups { get; }

    void AddGroup(string name);
    void DeleteGroup(GroupItem group);
    void MoveGroupUp(GroupItem group);
    void MoveGroupDown(GroupItem group);
    void Save();
    void Load();
}