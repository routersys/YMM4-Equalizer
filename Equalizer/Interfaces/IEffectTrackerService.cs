namespace Equalizer.Interfaces;

public interface IEffectTrackerService
{
    string GetLabel(EqualizerAudioEffect effect);
    void SelectPreset(EqualizerAudioEffect effect, string presetName);
    void ClearPreset(EqualizerAudioEffect effect);
    void RenamePreset(EqualizerAudioEffect effect, string newName);
    string GetSelectedPresetName(EqualizerAudioEffect effect);
    void UpdateModifiedState(EqualizerAudioEffect effect);
}