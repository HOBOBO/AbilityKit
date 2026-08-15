using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattlePlayerLoadoutStore
    {
        private readonly Dictionary<string, MobaPlayerLoadout> _runtimeLoadouts =
            new Dictionary<string, MobaPlayerLoadout>(StringComparer.OrdinalIgnoreCase);

        public int Revision { get; private set; }

        public bool Apply(
            in MobaPlayerHeroChangedSnapshotEntry entry,
            MobaPlayerLoadout[] startupLoadouts)
        {
            if (string.IsNullOrEmpty(entry.PlayerId) || entry.ActorId <= 0)
            {
                return false;
            }

            var source = Resolve(entry.PlayerId, startupLoadouts);
            _runtimeLoadouts[entry.PlayerId] = new MobaPlayerLoadout(
                new PlayerId(entry.PlayerId),
                entry.TeamId,
                entry.HeroId,
                entry.AttributeTemplateId,
                entry.Level > 0 ? entry.Level : 1,
                entry.BasicAttackSkillId,
                entry.SkillIds ?? Array.Empty<int>(),
                source.SpawnIndex,
                source.UnitSubType,
                source.MainType,
                source.HasSpawnPosition,
                source.SpawnX,
                source.SpawnY,
                source.SpawnZ,
                source.BrainId,
                source.EnableBrainOnSpawn);
            Revision++;
            return true;
        }

        public MobaPlayerLoadout[] BuildEffective(MobaPlayerLoadout[] startupLoadouts)
        {
            if (startupLoadouts == null || startupLoadouts.Length == 0)
            {
                return Array.Empty<MobaPlayerLoadout>();
            }

            var effective = new MobaPlayerLoadout[startupLoadouts.Length];
            for (var i = 0; i < startupLoadouts.Length; i++)
            {
                var playerId = startupLoadouts[i].PlayerId.Value;
                effective[i] = !string.IsNullOrEmpty(playerId) &&
                               _runtimeLoadouts.TryGetValue(playerId, out var runtime)
                    ? runtime
                    : startupLoadouts[i];
            }

            return effective;
        }

        public void Clear()
        {
            _runtimeLoadouts.Clear();
            Revision = 0;
        }

        private MobaPlayerLoadout Resolve(
            string playerId,
            MobaPlayerLoadout[] startupLoadouts)
        {
            if (_runtimeLoadouts.TryGetValue(playerId, out var runtime))
            {
                return runtime;
            }

            if (startupLoadouts != null)
            {
                for (var i = 0; i < startupLoadouts.Length; i++)
                {
                    if (string.Equals(
                            startupLoadouts[i].PlayerId.Value,
                            playerId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return startupLoadouts[i];
                    }
                }
            }

            return default;
        }
    }
}
