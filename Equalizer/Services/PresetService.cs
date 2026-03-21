using Equalizer.Interfaces;
using Equalizer.Models;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;

namespace Equalizer.Services;

public sealed class PresetService : IPresetService
{
    private readonly string _presetsDir;
    private readonly string _metadataPath;
    private readonly Lock _metadataLock = new();
    private Dictionary<string, PresetMetadata> _presetMetadata = [];

    private readonly JsonSerializerSettings _serializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        Formatting = Formatting.Indented
    };

    public event EventHandler? PresetsChanged;

    public PresetService()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        _presetsDir = Path.Combine(assemblyDir, "presets");
        var configDir = Path.Combine(assemblyDir, "Config");
        _metadataPath = Path.Combine(configDir, "_metadata.json");

        Directory.CreateDirectory(_presetsDir);
        Directory.CreateDirectory(configDir);

        LoadMetadata();
    }

    private void LoadMetadata()
    {
        lock (_metadataLock)
        {
            if (!File.Exists(_metadataPath)) return;

            try
            {
                var json = File.ReadAllText(_metadataPath);
                _presetMetadata = JsonConvert.DeserializeObject<Dictionary<string, PresetMetadata>>(json) ?? [];
            }
            catch
            {
                _presetMetadata = [];
            }
        }
    }

    private void SaveMetadata()
    {
        lock (_metadataLock)
        {
            try
            {
                var json = JsonConvert.SerializeObject(_presetMetadata, Formatting.Indented);
                File.WriteAllText(_metadataPath, json);
            }
            catch { }
        }
    }

    public IReadOnlyList<string> GetAllPresetNames()
    {
        if (!Directory.Exists(_presetsDir)) return [];

        return Directory.GetFiles(_presetsDir, "*.eqp")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }

    public PresetInfo GetPresetInfo(string name)
    {
        lock (_metadataLock)
        {
            var metadata = _presetMetadata.GetValueOrDefault(name, new PresetMetadata());
            return new PresetInfo
            {
                Name = name,
                Group = metadata.Group,
                IsFavorite = metadata.IsFavorite
            };
        }
    }

    public ObservableCollection<EQBand>? LoadPreset(string name)
    {
        var filePath = Path.Combine(_presetsDir, $"{name}.eqp");
        if (!File.Exists(filePath)) return null;

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<ObservableCollection<EQBand>>(json, _serializerSettings);
        }
        catch (Exception ex)
        {
            ShowError($"プリセットの読み込みに失敗しました。\n{ex.Message}");
            return null;
        }
    }

    public bool SavePreset(string name, IEnumerable<EQBand> bands)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            ShowError("無効なプリセット名です。");
            return false;
        }

        try
        {
            var json = JsonConvert.SerializeObject(bands, _serializerSettings);
            File.WriteAllText(Path.Combine(_presetsDir, $"{name}.eqp"), json);

            lock (_metadataLock)
            {
                _presetMetadata.TryAdd(name, new PresetMetadata());
            }

            SaveMetadata();
            RaisePresetsChanged();
            return true;
        }
        catch (Exception ex)
        {
            ShowError($"プリセットの保存に失敗しました。\n{ex.Message}");
            return false;
        }
    }

    public void DeletePreset(string name)
    {
        var filePath = Path.Combine(_presetsDir, $"{name}.eqp");
        if (!File.Exists(filePath)) return;

        File.Delete(filePath);

        lock (_metadataLock)
        {
            _presetMetadata.Remove(name);
        }

        SaveMetadata();
        RaisePresetsChanged();
    }

    public bool RenamePreset(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            ShowError("無効なプリセット名です。");
            return false;
        }

        var oldPath = Path.Combine(_presetsDir, $"{oldName}.eqp");
        var newPath = Path.Combine(_presetsDir, $"{newName}.eqp");

        if (File.Exists(newPath))
        {
            ShowError("同じ名前のプリセットが既に存在します。");
            return false;
        }

        if (!File.Exists(oldPath)) return false;

        File.Move(oldPath, newPath);

        lock (_metadataLock)
        {
            if (_presetMetadata.Remove(oldName, out var metadata))
            {
                _presetMetadata[newName] = metadata;
            }
        }

        SaveMetadata();
        RaisePresetsChanged();
        return true;
    }

    public void SetPresetGroup(string name, string group)
    {
        lock (_metadataLock)
        {
            if (!_presetMetadata.TryGetValue(name, out var metadata))
            {
                metadata = new PresetMetadata();
                _presetMetadata[name] = metadata;
            }
            metadata.Group = group;
        }

        SaveMetadata();
        RaisePresetsChanged();
    }

    public void SetPresetFavorite(string name, bool isFavorite)
    {
        lock (_metadataLock)
        {
            if (!_presetMetadata.TryGetValue(name, out var metadata))
            {
                metadata = new PresetMetadata();
                _presetMetadata[name] = metadata;
            }
            metadata.IsFavorite = isFavorite;
        }

        SaveMetadata();
        RaisePresetsChanged();
    }

    public bool ExportPreset(string name, string exportPath)
    {
        var sourcePath = Path.Combine(_presetsDir, $"{name}.eqp");
        if (!File.Exists(sourcePath)) return false;

        try
        {
            File.Copy(sourcePath, exportPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            ShowError($"プリセットのエクスポートに失敗しました。\n{ex.Message}");
            return false;
        }
    }

    public bool ImportPreset(string importPath, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        try
        {
            var json = File.ReadAllText(importPath);
            var bands = JsonConvert.DeserializeObject<ObservableCollection<EQBand>>(json, _serializerSettings);
            if (bands is null) return false;

            File.WriteAllText(Path.Combine(_presetsDir, $"{name}.eqp"), json);

            lock (_metadataLock)
            {
                _presetMetadata.TryAdd(name, new PresetMetadata { Group = "other" });
            }

            SaveMetadata();
            RaisePresetsChanged();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void RaisePresetsChanged() =>
        PresetsChanged?.Invoke(this, EventArgs.Empty);

    private static void ShowError(string message) =>
        MessageBox.Show(message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
}