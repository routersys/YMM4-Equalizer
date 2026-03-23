using Equalizer.Infrastructure;
using Equalizer.Interfaces;
using Equalizer.Localization;
using Equalizer.Models;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Text;

namespace Equalizer.Services;

public sealed class GroupService : IGroupService
{
    private static readonly GroupItem[] DefaultGroups =
    [
        new(Texts.DefaultGroupVocal, "vocal"),
        new("BGM", "bgm"),
        new(Texts.DefaultGroupSfx, "sfx"),
        new(Texts.DefaultGroupOther, "other")
    ];

    private readonly string _configPath;
    private ObservableCollection<GroupItem> _userGroups = [];

    public ObservableCollection<GroupItem> UserGroups => _userGroups;

    public GroupService()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        _configPath = Path.Combine(assemblyDir, "Config", "groups.json");
        Load();
    }

    public void Load()
    {
        _userGroups.CollectionChanged -= OnCollectionChanged;

        if (!TryLoadFromFile())
            InitializeDefaults();

        _userGroups.CollectionChanged += OnCollectionChanged;
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (dir is not null)
                Directory.CreateDirectory(dir);

            var json = JsonConvert.SerializeObject(_userGroups, Formatting.Indented);
            byte[] data = Encoding.UTF8.GetBytes(json);
            AtomicFileOperations.Write(_configPath, data);
        }
        catch { }
    }

    public void AddGroup(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_userGroups.Any(g => g.Name == name)) return;

        var newItem = new GroupItem(name, Guid.NewGuid().ToString());
        int otherIndex = FindOtherGroupIndex();

        if (otherIndex >= 0)
            _userGroups.Insert(otherIndex, newItem);
        else
            _userGroups.Add(newItem);
    }

    public void DeleteGroup(GroupItem group)
    {
        if (group is null || group.Tag == "other") return;
        _userGroups.Remove(group);
    }

    public void MoveGroupUp(GroupItem group)
    {
        if (group is null || group.Tag == "other") return;

        int index = _userGroups.IndexOf(group);
        if (index > 0)
            _userGroups.Move(index, index - 1);
    }

    public void MoveGroupDown(GroupItem group)
    {
        if (group is null || group.Tag == "other") return;

        int index = _userGroups.IndexOf(group);
        if (index >= 0 && index < _userGroups.Count - 2)
            _userGroups.Move(index, index + 1);
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Save();

    private bool TryLoadFromFile()
    {
        byte[]? data = AtomicFileOperations.ReadWithFallback(_configPath);
        if (data is null) return false;

        try
        {
            var json = Encoding.UTF8.GetString(data);
            var groups = JsonConvert.DeserializeObject<ObservableCollection<GroupItem>>(json);

            if (groups is not { Count: > 0 }) return false;

            _userGroups = groups;
            EnsureOtherGroupExists();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void InitializeDefaults()
    {
        _userGroups = new ObservableCollection<GroupItem>(
            DefaultGroups.Select(g => new GroupItem(g.Name, g.Tag)));
    }

    private void EnsureOtherGroupExists()
    {
        var other = _userGroups.FirstOrDefault(g => g.Tag == "other");
        if (other is null)
        {
            _userGroups.Add(new GroupItem(Texts.DefaultGroupOther, "other"));
            return;
        }

        int currentIndex = _userGroups.IndexOf(other);
        if (currentIndex != _userGroups.Count - 1)
        {
            _userGroups.Remove(other);
            _userGroups.Add(other);
        }
    }

    private int FindOtherGroupIndex() =>
        Enumerable.Range(0, _userGroups.Count)
            .FirstOrDefault(i => _userGroups[i].Tag == "other", -1);
}