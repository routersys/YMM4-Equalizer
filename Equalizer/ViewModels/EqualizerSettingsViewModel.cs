using Equalizer.Enums;
using Equalizer.Interfaces;
using Equalizer.Models;
using Equalizer.Services;
using Equalizer.Views;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Equalizer.ViewModels;

public sealed class EqualizerSettingsViewModel : ViewModelBase
{
    private readonly IPresetService _presetService;
    private readonly IGroupService _groupService;
    private PresetInfo? _selectedPreset;
    private string? _selectedDefaultPreset;
    private GroupItem _selectedGroupItem = default!;

    public ObservableCollection<PresetInfo> Presets { get; } = [];
    public ObservableCollection<string> AllPresetNames { get; } = [];
    public ObservableCollection<GroupItem> Groups { get; } = [];
    public IEnumerable<EqualizerAlgorithm> AlgorithmOptions => Enum.GetValues<EqualizerAlgorithm>();

    public GroupItem SelectedGroupItem
    {
        get => _selectedGroupItem;
        set
        {
            if (!SetProperty(ref _selectedGroupItem, value)) return;
            LoadData();
        }
    }

    public PresetInfo? SelectedPreset
    {
        get => _selectedPreset;
        set => SetProperty(ref _selectedPreset, value);
    }

    public string? SelectedDefaultPreset
    {
        get => _selectedDefaultPreset;
        set
        {
            if (!SetProperty(ref _selectedDefaultPreset, value) || value is null) return;
            EqualizerSettings.Default.DefaultPreset = value == "なし" ? "" : value;
            EqualizerSettings.Default.Save();
        }
    }

    public bool HighQualityMode
    {
        get => EqualizerSettings.Default.HighQualityMode;
        set
        {
            if (EqualizerSettings.Default.HighQualityMode == value) return;
            EqualizerSettings.Default.HighQualityMode = value;
            EqualizerSettings.Default.Save();
            OnPropertyChanged();
        }
    }

    public EqualizerAlgorithm SelectedAlgorithm
    {
        get => EqualizerSettings.Default.Algorithm;
        set
        {
            if (EqualizerSettings.Default.Algorithm == value) return;
            EqualizerSettings.Default.Algorithm = value;
            EqualizerSettings.Default.Save();
            OnPropertyChanged();
        }
    }

    public ICommand RenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ChangeGroupCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand ClearDefaultPresetCommand { get; }
    public ICommand AddGroupCommand { get; }
    public ICommand DeleteGroupCommand { get; }
    public ICommand MoveGroupUpCommand { get; }
    public ICommand MoveGroupDownCommand { get; }

    public EqualizerSettingsViewModel()
    {
        _presetService = ServiceLocator.PresetService;
        _groupService = ServiceLocator.GroupService;

        RenameCommand = new RelayCommand(_ => RenamePreset(), _ => SelectedPreset is not null);
        DeleteCommand = new RelayCommand(_ => DeletePreset(), _ => SelectedPreset is not null);
        ImportCommand = new RelayCommand(_ => ImportPreset());
        ExportCommand = new RelayCommand(_ => ExportPreset(), _ => SelectedPreset is not null);
        ChangeGroupCommand = new RelayCommand(_ => ChangeGroup(), _ => SelectedPreset is not null);
        ToggleFavoriteCommand = new RelayCommand(ToggleFavorite);
        ClearDefaultPresetCommand = new RelayCommand(_ => SelectedDefaultPreset = "なし");
        AddGroupCommand = new RelayCommand(_ => AddGroup());
        DeleteGroupCommand = new RelayCommand(_ => DeleteGroup(), _ => IsUserGroup(SelectedGroupItem));
        MoveGroupUpCommand = new RelayCommand(_ => MoveGroupUp(), _ => IsUserGroup(SelectedGroupItem) && CanMoveUp(SelectedGroupItem));
        MoveGroupDownCommand = new RelayCommand(_ => MoveGroupDown(), _ => IsUserGroup(SelectedGroupItem) && CanMoveDown(SelectedGroupItem));

        _presetService.PresetsChanged += (_, _) => LoadData();
        _groupService.UserGroups.CollectionChanged += (_, _) => RefreshGroups();

        RefreshGroups();
        LoadData();
    }

    private static bool IsUserGroup(GroupItem? item) =>
        item?.Tag is not (null or "" or "favorites" or "other");

    private bool CanMoveUp(GroupItem item) =>
        _groupService.UserGroups.IndexOf(item) > 0;

    private bool CanMoveDown(GroupItem item)
    {
        int idx = _groupService.UserGroups.IndexOf(item);
        return idx >= 0 && idx < _groupService.UserGroups.Count - 2;
    }

    private void RefreshGroups()
    {
        var currentTag = SelectedGroupItem?.Tag;
        Groups.Clear();
        Groups.Add(new GroupItem("すべて", ""));
        Groups.Add(new GroupItem("お気に入り", "favorites"));

        foreach (var group in _groupService.UserGroups)
            Groups.Add(group);

        SelectedGroupItem = Groups.FirstOrDefault(g => g.Tag == currentTag) ?? Groups[0];
    }

    private void LoadData()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var prevName = SelectedPreset?.Name;
            Presets.Clear();
            AllPresetNames.Clear();
            AllPresetNames.Add("なし");

            var allNames = _presetService.GetAllPresetNames();
            IEnumerable<PresetInfo> filtered = allNames.Select(_presetService.GetPresetInfo);

            if (SelectedGroupItem?.Tag is { Length: > 0 } tag)
            {
                filtered = tag == "favorites"
                    ? filtered.Where(p => p.IsFavorite)
                    : filtered.Where(p => p.Group == tag);
            }

            foreach (var p in filtered.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
                Presets.Add(p);

            foreach (var n in allNames)
                AllPresetNames.Add(n);

            var defaultPreset = EqualizerSettings.Default.DefaultPreset;
            _selectedDefaultPreset = string.IsNullOrEmpty(defaultPreset) || !allNames.Contains(defaultPreset)
                ? "なし"
                : defaultPreset;
            OnPropertyChanged(nameof(SelectedDefaultPreset));

            if (prevName is not null)
                SelectedPreset = Presets.FirstOrDefault(p => p.Name == prevName);
        });
    }

    private void RenamePreset()
    {
        if (SelectedPreset is null) return;

        var dialog = new InputDialogWindow("新しいプリセット名", "プリセット名の変更", SelectedPreset.Name)
        {
            Owner = ResolveActiveWindow()
        };

        if (dialog.ShowDialog() != true) return;
        if (string.IsNullOrWhiteSpace(dialog.InputText) || dialog.InputText == SelectedPreset.Name) return;
        _presetService.RenamePreset(SelectedPreset.Name, dialog.InputText);
    }

    private void DeletePreset()
    {
        if (SelectedPreset is null) return;

        var result = MessageBox.Show(
            $"プリセット「{SelectedPreset.Name}」を削除しますか？",
            "確認",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
            _presetService.DeletePreset(SelectedPreset.Name);
    }

    private void ImportPreset()
    {
        var dialog = new OpenFileDialog
        {
            Title = "プリセットファイルを選択",
            Filter = "EQPファイル (*.eqp)|*.eqp|すべてのファイル (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true) return;

        foreach (var file in dialog.FileNames)
            _presetService.ImportPreset(file, Path.GetFileNameWithoutExtension(file));
    }

    private void ExportPreset()
    {
        if (SelectedPreset is null) return;

        var dialog = new SaveFileDialog
        {
            Title = "プリセットをエクスポート",
            Filter = "EQPファイル (*.eqp)|*.eqp",
            FileName = $"{SelectedPreset.Name}.eqp"
        };

        if (dialog.ShowDialog() == true)
            _presetService.ExportPreset(SelectedPreset.Name, dialog.FileName);
    }

    private void ChangeGroup()
    {
        if (SelectedPreset is null) return;

        var dialog = new GroupSelectionWindow(SelectedPreset.Group)
        {
            Owner = ResolveActiveWindow()
        };

        if (dialog.ShowDialog() == true)
            _presetService.SetPresetGroup(SelectedPreset.Name, dialog.SelectedGroup?.Tag ?? "");
    }

    private void AddGroup()
    {
        var dialog = new InputDialogWindow("グループ名を入力してください", "グループ追加")
        {
            Owner = ResolveActiveWindow()
        };

        if (dialog.ShowDialog() == true)
            _groupService.AddGroup(dialog.InputText);
    }

    private void DeleteGroup()
    {
        if (SelectedGroupItem is null || !IsUserGroup(SelectedGroupItem)) return;

        var result = MessageBox.Show(
            $"グループ「{SelectedGroupItem.Name}」を削除しますか？",
            "確認",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
            _groupService.DeleteGroup(SelectedGroupItem);
    }

    private void MoveGroupUp()
    {
        if (SelectedGroupItem is not null)
            _groupService.MoveGroupUp(SelectedGroupItem);
    }

    private void MoveGroupDown()
    {
        if (SelectedGroupItem is not null)
            _groupService.MoveGroupDown(SelectedGroupItem);
    }

    private void ToggleFavorite(object? parameter)
    {
        if (parameter is PresetInfo info)
            _presetService.SetPresetFavorite(info.Name, !info.IsFavorite);
    }

    private static Window ResolveActiveWindow() =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current.MainWindow;
}