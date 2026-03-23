using Equalizer.Enums;
using Equalizer.Infrastructure;
using Equalizer.Interfaces;
using Equalizer.Localization;
using Equalizer.Models;
using Equalizer.Services;
using Equalizer.Views;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace Equalizer.ViewModels;

public sealed class EqualizerEditorViewModel : ViewModelBase
{
    private readonly IPresetService _presetService;
    private readonly IGroupService _groupService;
    private readonly IUserNotificationService _notifications;

    private EqualizerAudioEffect? _effect;
    private ObservableCollection<EQBand>? _bands;
    private EQBand? _selectedBand;
    private string _selectedPresetName = Texts.SelectPresetPlaceholder;
    private double _zoom = 24;
    private double _currentTime;
    private string _currentGroupFilter = "";
    private bool _isPopupOpen;
    private GroupItem _selectedGroupItem = default!;

    public event EventHandler? RequestRedraw;
    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    public EqualizerAudioEffect? Effect
    {
        get => _effect;
        set
        {
            if (!SetProperty(ref _effect, value) || _effect is null) return;
            Bands = _effect.Bands;
        }
    }

    public ObservableCollection<EQBand>? Bands
    {
        get => _bands;
        set
        {
            if (SetProperty(ref _bands, value))
                OnPropertyChanged(nameof(HasBands));
        }
    }

    public bool HasBands => Bands is { Count: > 0 };

    public EQBand? SelectedBand
    {
        get => _selectedBand;
        set => SetProperty(ref _selectedBand, value);
    }

    public string SelectedPresetName
    {
        get => _selectedPresetName;
        set => SetProperty(ref _selectedPresetName, value);
    }

    public double Zoom
    {
        get => _zoom;
        set
        {
            if (!SetProperty(ref _zoom, value)) return;
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }
    }

    public double CurrentTime
    {
        get => _currentTime;
        set
        {
            if (!SetProperty(ref _currentTime, value)) return;
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }
    }

    public double EditorHeight
    {
        get => EqualizerSettings.Default.EditorHeight;
        set
        {
            EqualizerSettings.Default.EditorHeight = value;
            OnPropertyChanged();
        }
    }

    public bool IsPopupOpen
    {
        get => _isPopupOpen;
        set => SetProperty(ref _isPopupOpen, value);
    }

    public ObservableCollection<PresetInfo> FilteredPresets { get; } = [];
    public ObservableCollection<GroupItem> Groups { get; } = [];

    public GroupItem SelectedGroupItem
    {
        get => _selectedGroupItem;
        set
        {
            if (!SetProperty(ref _selectedGroupItem, value)) return;
            _currentGroupFilter = value?.Tag ?? "";
            LoadPresets();
        }
    }

    public ICommand SavePresetCommand { get; }
    public ICommand RenamePresetCommand { get; }
    public ICommand DeletePresetCommand { get; }
    public ICommand LoadPresetCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand AddPointCommand { get; }
    public ICommand DeletePointCommand { get; }
    public ICommand ChangeGroupCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand AddGroupCommand { get; }
    public ICommand DeleteGroupCommand { get; }

    public EqualizerEditorViewModel()
        : this(ServiceLocator.PresetService, ServiceLocator.GroupService, ServiceLocator.NotificationService) { }

    public EqualizerEditorViewModel(
        IPresetService presetService,
        IGroupService groupService,
        IUserNotificationService notifications)
    {
        _presetService = presetService;
        _groupService = groupService;
        _notifications = notifications;

        _presetService.PresetsChanged += (_, _) => LoadPresets();
        _groupService.UserGroups.CollectionChanged += (_, _) => RefreshGroups();

        SavePresetCommand = new AsyncRelayCommand(_ => SavePresetAsync(), _ => HasBands);
        RenamePresetCommand = new AsyncRelayCommand(RenamePresetAsync, p => p is PresetInfo);
        DeletePresetCommand = new AsyncRelayCommand(DeletePresetAsync, p => p is PresetInfo);
        LoadPresetCommand = new RelayCommand(LoadPreset, p => p is PresetInfo);
        ToggleFavoriteCommand = new RelayCommand(ToggleFavorite, p => p is PresetInfo);
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        AddPointCommand = new RelayCommand(AddPoint, p => p is Point or null);
        DeletePointCommand = new RelayCommand(DeletePoint, p => p is EQBand);
        ChangeGroupCommand = new AsyncRelayCommand(ChangeGroupAsync, p => p is PresetInfo);
        ExportCommand = new RelayCommand(ExportPreset, p => p is PresetInfo);
        AddGroupCommand = new AsyncRelayCommand(_ => AddGroupAsync());
        DeleteGroupCommand = new AsyncRelayCommand(
            DeleteGroupAsync,
            p => p is GroupItem { Tag: not (null or "" or "favorites" or "other") });

        RefreshGroups();
        LoadPresets();
    }

    public EditScope CreateEditScope() =>
        EditScope.Begin(NotifyBeginEdit, NotifyEndEdit);

    public void NotifyBeginEdit() => BeginEdit?.Invoke(this, EventArgs.Empty);
    public void NotifyEndEdit() => EndEdit?.Invoke(this, EventArgs.Empty);
    public void NotifyRedraw() => RequestRedraw?.Invoke(this, EventArgs.Empty);

    private void RefreshGroups()
    {
        var currentTag = SelectedGroupItem?.Tag;
        Groups.Clear();
        Groups.Add(new GroupItem(Texts.AllGroup, ""));
        Groups.Add(new GroupItem(Texts.FavoritesGroup, "favorites"));

        foreach (var group in _groupService.UserGroups)
            Groups.Add(group);

        _selectedGroupItem = Groups.FirstOrDefault(g => g.Tag == currentTag) ?? Groups[0];
        _currentGroupFilter = _selectedGroupItem.Tag;
        OnPropertyChanged(nameof(SelectedGroupItem));
    }

    private void LoadPresets()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            FilteredPresets.Clear();

            IEnumerable<PresetInfo> query = _presetService.GetAllPresetNames()
                .Select(_presetService.GetPresetInfo);

            if (_currentGroupFilter == "favorites")
                query = query.Where(p => p.IsFavorite);
            else if (!string.IsNullOrEmpty(_currentGroupFilter))
                query = query.Where(p => p.Group == _currentGroupFilter);

            foreach (var preset in query.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
                FilteredPresets.Add(preset);
        });
    }

    private void LoadPreset(object? parameter)
    {
        if (parameter is not PresetInfo info || Effect is null) return;

        var bands = _presetService.LoadPreset(info.Name);
        if (bands is null) return;

        using (CreateEditScope())
        {
            Effect.ApplyBands(bands);
            SelectedPresetName = info.Name;
            IsPopupOpen = false;
        }

        NotifyRedraw();
    }

    private async Task SavePresetAsync()
    {
        if (!HasBands) return;

        var name = await _notifications.PromptAsync(Texts.EnterPresetNamePrompt, Texts.SavePresetTitle);
        if (string.IsNullOrWhiteSpace(name)) return;

        if (_presetService.SavePreset(name, Bands!))
        {
            SelectedPresetName = name;
            LoadPresets();
        }
    }

    private async Task RenamePresetAsync(object? parameter)
    {
        if (parameter is not PresetInfo info) return;

        var newName = await _notifications.PromptAsync(
            Texts.EnterNewPresetNamePrompt, Texts.RenamePresetTitle, info.Name);

        if (string.IsNullOrWhiteSpace(newName) || newName == info.Name) return;

        if (_presetService.RenamePreset(info.Name, newName) && SelectedPresetName == info.Name)
            SelectedPresetName = newName;

        LoadPresets();
    }

    private async Task DeletePresetAsync(object? parameter)
    {
        if (parameter is not PresetInfo info) return;

        bool confirmed = await _notifications.ConfirmAsync(
            string.Format(Texts.DeletePresetConfirm, info.Name),
            Texts.Confirmation);

        if (!confirmed) return;

        _presetService.DeletePreset(info.Name);

        if (SelectedPresetName == info.Name)
            SelectedPresetName = Texts.SelectPresetPlaceholder;

        LoadPresets();
    }

    private void ExportPreset(object? parameter)
    {
        if (parameter is not PresetInfo info) return;

        var dialog = new SaveFileDialog
        {
            Title = Texts.ExportPresetTitle,
            Filter = Texts.EqpFileFilter,
            FileName = $"{info.Name}.eqp"
        };

        if (dialog.ShowDialog() == true)
            _presetService.ExportPreset(info.Name, dialog.FileName);
    }

    private Task ChangeGroupAsync(object? parameter)
    {
        if (parameter is not PresetInfo info) return Task.CompletedTask;

        var dialog = new GroupSelectionWindow(info.Group)
        {
            Owner = ResolveActiveWindow()
        };

        if (dialog.ShowDialog() == true)
            _presetService.SetPresetGroup(info.Name, dialog.SelectedGroup?.Tag ?? "");

        return Task.CompletedTask;
    }

    private async Task AddGroupAsync()
    {
        var name = await _notifications.PromptAsync(Texts.EnterGroupNamePrompt, Texts.AddGroup);
        if (!string.IsNullOrWhiteSpace(name))
            _groupService.AddGroup(name);
    }

    private async Task DeleteGroupAsync(object? parameter)
    {
        if (parameter is not GroupItem item) return;

        if (item.Tag is "favorites" or "" or "other")
        {
            _notifications.ShowError(Texts.CannotDeleteGroup);
            return;
        }

        bool confirmed = await _notifications.ConfirmAsync(
            string.Format(Texts.DeleteGroupConfirm, item.Name),
            Texts.Confirmation);

        if (confirmed)
            _groupService.DeleteGroup(item);
    }

    private void ToggleFavorite(object? parameter)
    {
        if (parameter is PresetInfo info)
            _presetService.SetPresetFavorite(info.Name, !info.IsFavorite);
    }

    private void OpenSettings()
    {
        var window = new EqualizerSettingsWindow
        {
            Owner = ResolveActiveWindow(),
            DataContext = new EqualizerSettingsViewModel(),
            Topmost = true
        };
        window.ShowDialog();
    }

    private void AddPoint(object? parameter)
    {
        if (parameter is not Point point || Effect is null) return;

        using (CreateEditScope())
        {
            var unusedBand = Effect.Items.FirstOrDefault(b => !b.IsUsed);
            if (unusedBand is not null)
            {
                unusedBand.IsUsed = true;
                unusedBand.IsEnabled = true;
                unusedBand.Type = FilterType.Peak;
                unusedBand.Frequency.Values[0].Value = point.X;
                unusedBand.Gain.Values[0].Value = point.Y;
                unusedBand.Q.Values[0].Value = 1.0;
                unusedBand.StereoMode = StereoMode.Stereo;
                Effect.UpdateBandsCollection();
                SelectedBand = unusedBand;
            }
            else
            {
                _notifications.ShowWarning(Texts.MaxPointsReached);
            }
        }

        NotifyRedraw();
    }

    private void DeletePoint(object? parameter)
    {
        if (parameter is not EQBand band || Effect is null) return;

        using (CreateEditScope())
        {
            int index = Bands?.IndexOf(band) ?? -1;
            band.IsUsed = false;
            Effect.UpdateBandsCollection();
            SelectedBand = Bands?.Count > 0
                ? (index > 0 ? Bands[index - 1] : Bands.FirstOrDefault())
                : null;
        }

        NotifyRedraw();
    }

    private static Window ResolveActiveWindow() =>
        Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive)
        ?? Application.Current.MainWindow;
}