using Equalizer.Interfaces;
using Equalizer.Localization;
using System.Runtime.CompilerServices;
using YukkuriMovieMaker.Commons;

namespace Equalizer.Services;

public sealed class EffectTrackerService : IEffectTrackerService
{
    private sealed class EffectState
    {
        public string PresetName { get; set; } = "";
        public int PresetHash { get; set; }
        public bool IsModified { get; set; }
        public int BandCount { get; set; }
        public string CachedLabel { get; set; } = "";
        public bool LabelDirty { get; set; } = true;
    }

    private readonly ConditionalWeakTable<EqualizerAudioEffect, EffectState> _states = [];
    private readonly Lock _lock = new();

    public string GetLabel(EqualizerAudioEffect effect)
    {
        var state = GetOrCreateState(effect);

        lock (_lock)
        {
            if (state.LabelDirty || state.BandCount != effect.Bands.Count)
            {
                state.BandCount = effect.Bands.Count;
                state.CachedLabel = BuildLabel(state, state.BandCount);
                state.LabelDirty = false;
            }
            return state.CachedLabel;
        }
    }

    public string GetSelectedPresetName(EqualizerAudioEffect effect)
    {
        var state = GetOrCreateState(effect);
        lock (_lock) return state.PresetName;
    }

    public void SelectPreset(EqualizerAudioEffect effect, string presetName)
    {
        var state = GetOrCreateState(effect);
        lock (_lock)
        {
            state.PresetName = presetName;
            state.PresetHash = ComputeStateHash(effect);
            state.IsModified = false;
            state.LabelDirty = true;
        }
    }

    public void ClearPreset(EqualizerAudioEffect effect)
    {
        var state = GetOrCreateState(effect);
        lock (_lock)
        {
            state.PresetName = "";
            state.PresetHash = 0;
            state.IsModified = false;
            state.LabelDirty = true;
        }
    }

    public void RenamePreset(EqualizerAudioEffect effect, string newName)
    {
        var state = GetOrCreateState(effect);
        lock (_lock)
        {
            if (string.IsNullOrEmpty(state.PresetName)) return;
            state.PresetName = newName;
            state.LabelDirty = true;
        }
    }

    public void UpdateModifiedState(EqualizerAudioEffect effect)
    {
        var state = GetOrCreateState(effect);
        lock (_lock)
        {
            if (string.IsNullOrEmpty(state.PresetName))
            {
                if (state.IsModified)
                {
                    state.IsModified = false;
                    state.LabelDirty = true;
                }
                return;
            }

            var currentHash = ComputeStateHash(effect);
            bool isModified = state.PresetHash != currentHash;

            if (state.IsModified != isModified)
            {
                state.IsModified = isModified;
                state.LabelDirty = true;
            }
        }
    }

    private EffectState GetOrCreateState(EqualizerAudioEffect effect)
    {
        return _states.GetValue(effect, _ => new EffectState());
    }

    private string BuildLabel(EffectState state, int bandCount)
    {
        var pointInfo = string.Format(Texts.EffectLabelPointCount, bandCount);

        if (string.IsNullOrEmpty(state.PresetName))
            return $"EXTENDED EQUALIZER - {pointInfo}";

        var modified = state.IsModified ? $" {Texts.EffectLabelPresetModified}" : "";
        return $"EXTENDED EQUALIZER - {state.PresetName}{modified} ({pointInfo})";
    }

    private int ComputeStateHash(EqualizerAudioEffect effect)
    {
        var hash = new HashCode();

        for (int i = 0; i < EqualizerAudioEffect.MaxBands; i++)
        {
            var band = effect.Items[i];
            if (!band.IsUsed) continue;

            hash.Add(i);
            hash.Add(band.IsEnabled);
            hash.Add((int)band.Type);
            hash.Add((int)band.StereoMode);
            hash.Add(GetAnimationValue(band.Frequency));
            hash.Add(GetAnimationValue(band.Gain));
            hash.Add(GetAnimationValue(band.Q));
        }

        return hash.ToHashCode();
    }

    private static float GetAnimationValue(Animation animation)
    {
        if (animation.Values.Count == 0) return 0f;
        return (float)animation.Values[0].Value;
    }
}