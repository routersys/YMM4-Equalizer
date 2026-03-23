using Equalizer.Infrastructure;
using Equalizer.Interfaces;
using Equalizer.Localization;
using Equalizer.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;

namespace Equalizer.Services;

public sealed class PresetService : IPresetService
{
    private const string PresetExtension = ".eqp";
    private const string MetadataFileName = "_metadata.eqmd";
    private const string LegacyMetadataFileName = "_metadata.json";

    private readonly string _presetsDir;
    private readonly string _metadataPath;
    private readonly string _legacyMetadataPath;
    private readonly IUserNotificationService _notifications;
    private readonly Lock _metadataLock = new();
    private readonly Lock _initLock = new();

    private Dictionary<string, PresetMetadata> _metadata = [];
    private volatile bool _initialized;

    public event EventHandler? PresetsChanged;

    public PresetService(IUserNotificationService notifications)
    {
        _notifications = notifications;

        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        _presetsDir = Path.Combine(assemblyDir, "presets");
        _metadataPath = Path.Combine(_presetsDir, MetadataFileName);
        _legacyMetadataPath = Path.Combine(_presetsDir, LegacyMetadataFileName);
    }

    public IReadOnlyList<string> GetAllPresetNames()
    {
        EnsureInitialized();

        if (!Directory.Exists(_presetsDir)) return [];

        return Directory.GetFiles(_presetsDir, "*" + PresetExtension)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }

    public PresetInfo GetPresetInfo(string name)
    {
        EnsureInitialized();

        lock (_metadataLock)
        {
            var meta = _metadata.GetValueOrDefault(name, new PresetMetadata());
            return new PresetInfo
            {
                Name = name,
                Group = meta.Group,
                IsFavorite = meta.IsFavorite
            };
        }
    }

    public ObservableCollection<EQBand>? LoadPreset(string name)
    {
        EnsureInitialized();

        string filePath = PresetPath(name);
        byte[]? fileData = AtomicFileOperations.ReadWithFallback(filePath);

        if (fileData is null)
        {
            _notifications.ShowError($"{Texts.PresetLoadFailed}\n{name}");
            return null;
        }

        var result = PresetFileFormat.Deserialize(fileData);

        if (result is null)
        {
            _notifications.ShowError($"{Texts.PresetLoadFailed}\n{name}");
            return null;
        }

        return result;
    }

    public bool SavePreset(string name, IEnumerable<EQBand> bands)
    {
        EnsureInitialized();

        if (!IsValidPresetName(name))
        {
            _notifications.ShowError(Texts.InvalidPresetName);
            return false;
        }

        try
        {
            byte[] data = PresetFileFormat.Serialize(bands);
            AtomicFileOperations.Write(PresetPath(name), data);

            lock (_metadataLock)
                _metadata.TryAdd(name, new PresetMetadata());

            PersistMetadata();
            RaisePresetsChanged();
            return true;
        }
        catch (Exception ex)
        {
            _notifications.ShowError($"{Texts.PresetSaveFailed}\n{ex.Message}");
            return false;
        }
    }

    public void DeletePreset(string name)
    {
        EnsureInitialized();

        string filePath = PresetPath(name);
        if (!File.Exists(filePath)) return;

        try
        {
            File.Delete(filePath);
            TryCleanupSidecarFiles(filePath);
        }
        catch (Exception ex)
        {
            _notifications.ShowError($"{Texts.PresetSaveFailed}\n{ex.Message}");
            return;
        }

        lock (_metadataLock)
            _metadata.Remove(name);

        PersistMetadata();
        RaisePresetsChanged();
    }

    public bool RenamePreset(string oldName, string newName)
    {
        EnsureInitialized();

        if (!IsValidPresetName(newName))
        {
            _notifications.ShowError(Texts.InvalidPresetName);
            return false;
        }

        string oldPath = PresetPath(oldName);
        string newPath = PresetPath(newName);

        if (File.Exists(newPath))
        {
            _notifications.ShowError(Texts.PresetNameAlreadyExists);
            return false;
        }

        if (!File.Exists(oldPath)) return false;

        try
        {
            File.Move(oldPath, newPath);
            TryMoveSidecarFiles(oldPath, newPath);
        }
        catch (Exception ex)
        {
            _notifications.ShowError($"{Texts.PresetSaveFailed}\n{ex.Message}");
            return false;
        }

        lock (_metadataLock)
        {
            if (_metadata.Remove(oldName, out var meta))
                _metadata[newName] = meta;
        }

        PersistMetadata();
        RaisePresetsChanged();
        return true;
    }

    public void SetPresetGroup(string name, string group)
    {
        EnsureInitialized();

        lock (_metadataLock)
            GetOrCreateMeta(name).Group = group;

        PersistMetadata();
        RaisePresetsChanged();
    }

    public void SetPresetFavorite(string name, bool isFavorite)
    {
        EnsureInitialized();

        lock (_metadataLock)
            GetOrCreateMeta(name).IsFavorite = isFavorite;

        PersistMetadata();
        RaisePresetsChanged();
    }

    public bool ExportPreset(string name, string exportPath)
    {
        EnsureInitialized();

        string sourcePath = PresetPath(name);
        if (!File.Exists(sourcePath)) return false;

        try
        {
            byte[]? data = AtomicFileOperations.ReadWithFallback(sourcePath);
            if (data is null) return false;

            AtomicFileOperations.Write(exportPath, data);
            return true;
        }
        catch (Exception ex)
        {
            _notifications.ShowError($"{Texts.PresetExportFailed}\n{ex.Message}");
            return false;
        }
    }

    public bool ImportPreset(string importPath, string name)
    {
        EnsureInitialized();

        if (!IsValidPresetName(name)) return false;

        try
        {
            byte[] importData = File.ReadAllBytes(importPath);

            if (PresetFileFormat.Deserialize(importData) is null) return false;

            AtomicFileOperations.Write(PresetPath(name), importData);

            lock (_metadataLock)
                _metadata.TryAdd(name, new PresetMetadata { Group = "other" });

            PersistMetadata();
            RaisePresetsChanged();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_initLock)
        {
            if (_initialized) return;
            Directory.CreateDirectory(_presetsDir);
            LoadMetadata();
            _initialized = true;
        }
    }

    private void LoadMetadata()
    {
        lock (_metadataLock)
        {
            var data = AtomicFileOperations.ReadWithFallback(_metadataPath);
            if (data is not null)
            {
                var deserialized = BinaryMetadataStore.Deserialize(data);
                if (deserialized is not null)
                {
                    _metadata = deserialized;
                    return;
                }
            }

            var migrated = BinaryMetadataStore.DeserializeLegacyJson(_legacyMetadataPath);
            if (migrated is not null)
            {
                _metadata = migrated;
                PersistMetadataLocked();
                TryArchiveLegacyMetadata();
                return;
            }

            _metadata = [];
        }
    }

    private void PersistMetadata()
    {
        lock (_metadataLock)
            PersistMetadataLocked();
    }

    private void PersistMetadataLocked()
    {
        try
        {
            byte[] data = BinaryMetadataStore.Serialize(_metadata);
            AtomicFileOperations.Write(_metadataPath, data);
        }
        catch { }
    }

    private void TryArchiveLegacyMetadata()
    {
        try
        {
            if (File.Exists(_legacyMetadataPath))
                File.Move(_legacyMetadataPath, _legacyMetadataPath + ".migrated", overwrite: true);
        }
        catch { }
    }

    private static void TryCleanupSidecarFiles(string presetPath)
    {
        foreach (string ext in new[] { ".tmp", ".bak" })
        {
            try { File.Delete(presetPath + ext); } catch { }
        }
    }

    private static void TryMoveSidecarFiles(string oldPath, string newPath)
    {
        string bakOld = oldPath + ".bak";
        string bakNew = newPath + ".bak";
        if (File.Exists(bakOld))
        {
            try { File.Move(bakOld, bakNew, overwrite: true); } catch { }
        }
    }

    private PresetMetadata GetOrCreateMeta(string name)
    {
        if (!_metadata.TryGetValue(name, out var meta))
        {
            meta = new PresetMetadata();
            _metadata[name] = meta;
        }
        return meta;
    }

    private string PresetPath(string name) =>
        Path.Combine(_presetsDir, name + PresetExtension);

    private static bool IsValidPresetName(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private void RaisePresetsChanged() =>
        PresetsChanged?.Invoke(this, EventArgs.Empty);
}