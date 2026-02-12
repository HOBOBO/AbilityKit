using System.Collections.Generic;

namespace AbilityKit.Ability.Host.Extensions.Moba.StartSources
{
    public sealed class InMemoryMobaDungeonPresetResolver : IMobaDungeonPresetResolver
    {
        private readonly Dictionary<long, MobaDungeonPreset> _presets = new Dictionary<long, MobaDungeonPreset>();

        public void Register(in MobaDungeonPreset preset)
        {
            var key = PackKey(preset.DungeonId, preset.PresetId);
            _presets[key] = preset;
        }

        public bool TryResolve(int dungeonId, int presetId, out MobaDungeonPreset preset)
        {
            return _presets.TryGetValue(PackKey(dungeonId, presetId), out preset);
        }

        private static long PackKey(int dungeonId, int presetId)
        {
            return ((long)dungeonId << 32) | (uint)presetId;
        }
    }
}
