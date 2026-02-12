using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Struct;

namespace AbilityKit.Ability.Host.Extensions.Moba.StartSources
{
    public sealed class DungeonPresetGameStartSource : IMobaGameStartSource
    {
        private readonly IMobaDungeonPresetResolver _resolver;
        private readonly int _dungeonId;
        private readonly int _presetId;

        public MobaGameStartSourceKind Kind => MobaGameStartSourceKind.DungeonPreset;

        public DungeonPresetGameStartSource(IMobaDungeonPresetResolver resolver, int dungeonId, int presetId)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _dungeonId = dungeonId;
            _presetId = presetId;
        }

        public bool TryBuild(PlayerId localPlayerId, out MobaRoomGameStartSpec spec)
        {
            if (string.IsNullOrEmpty(localPlayerId.Value))
            {
                spec = default;
                return false;
            }

            if (!_resolver.TryResolve(_dungeonId, _presetId, out var preset))
            {
                spec = default;
                return false;
            }

            var ov = new MobaRoomLoadoutOverrides(
                level: preset.Level,
                attributeTemplateId: preset.AttributeTemplateId,
                basicAttackSkillId: preset.BasicAttackSkillId,
                skillIds: preset.SkillIds);

            var player = new MobaRoomPlayerSlot(
                playerId: localPlayerId,
                teamId: preset.TeamId,
                heroId: preset.HeroId,
                spawnPointId: preset.SpawnPointId,
                overrides: in ov);

            spec = new MobaRoomGameStartSpec(
                matchId: !string.IsNullOrEmpty(preset.MatchId) ? preset.MatchId : $"dungeon_{preset.DungeonId}_{preset.PresetId}",
                mapId: preset.MapId,
                randomSeed: preset.RandomSeed,
                tickRate: preset.TickRate,
                inputDelayFrames: preset.InputDelayFrames,
                players: new[] { player });

            return true;
        }
    }
}
