using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleContext
    {
        private Dictionary<string, MobaPlayerLoadout> _runtimePlayerLoadouts;

        public BattleLogicSession Session;
        public IWorld RuntimeWorld;
        public BattleStartPlan Plan;
        public int LastFrame;
        public double LogicTimeSeconds;
        public int LocalActorId;
        public string LocalControlPlayerId;
        public BattleSessionHooks Hooks;
        public bool CanSubmitGameplayInput = true;
        public int RuntimePlayerLoadoutRevision { get; private set; }

        public bool TryGetRuntimeWorld(out IWorld world)
        {
            world = RuntimeWorld;
            if (world != null)
            {
                return true;
            }

            return Session != null &&
                   Session.TryGetWorld(out world) &&
                   world != null;
        }

        public string ResolveLocalControlPlayerId()
        {
            if (!string.IsNullOrEmpty(LocalControlPlayerId)) return LocalControlPlayerId;
            if (!string.IsNullOrEmpty(Plan.World.PlayerId)) return Plan.World.PlayerId;
            return Plan.LaunchSpec.LocalPlayerId.Value;
        }

        public void ApplyPlayerHeroChanged(in MobaPlayerHeroChangedSnapshotEntry entry)
        {
            if (string.IsNullOrEmpty(entry.PlayerId) || entry.ActorId <= 0) return;

            var source = ResolvePlayerLoadout(entry.PlayerId);
            var loadout = new MobaPlayerLoadout(
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

            _runtimePlayerLoadouts ??=
                new Dictionary<string, MobaPlayerLoadout>(StringComparer.OrdinalIgnoreCase);
            _runtimePlayerLoadouts[entry.PlayerId] = loadout;
            RuntimePlayerLoadoutRevision++;

            if (string.Equals(
                    ResolveLocalControlPlayerId(),
                    entry.PlayerId,
                    StringComparison.OrdinalIgnoreCase))
            {
                LocalActorId = entry.ActorId;
            }
        }

        public MobaPlayerLoadout[] BuildEffectivePlayerLoadouts()
        {
            var startup = Plan.LaunchSpec.Players;
            if (startup == null || startup.Length == 0) return Array.Empty<MobaPlayerLoadout>();

            var effective = new MobaPlayerLoadout[startup.Length];
            for (var i = 0; i < startup.Length; i++)
            {
                var playerId = startup[i].PlayerId.Value;
                effective[i] = _runtimePlayerLoadouts != null &&
                               !string.IsNullOrEmpty(playerId) &&
                               _runtimePlayerLoadouts.TryGetValue(playerId, out var runtime)
                    ? runtime
                    : startup[i];
            }

            return effective;
        }

        private MobaPlayerLoadout ResolvePlayerLoadout(string playerId)
        {
            if (_runtimePlayerLoadouts != null &&
                _runtimePlayerLoadouts.TryGetValue(playerId, out var runtime))
            {
                return runtime;
            }

            var startup = Plan.LaunchSpec.Players;
            if (startup != null)
            {
                for (var i = 0; i < startup.Length; i++)
                {
                    if (string.Equals(
                            startup[i].PlayerId.Value,
                            playerId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return startup[i];
                    }
                }
            }

            return default;
        }

        private void ClearRuntimePlayerLoadouts()
        {
            _runtimePlayerLoadouts?.Clear();
            RuntimePlayerLoadoutRevision = 0;
        }
    }
}
