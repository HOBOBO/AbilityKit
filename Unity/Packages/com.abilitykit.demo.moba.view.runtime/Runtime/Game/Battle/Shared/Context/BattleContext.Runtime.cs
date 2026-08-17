using System;
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
        BattleHostMode IBattleInputSessionIdentityPort.HostMode => Plan.HostMode;

        string IBattleInputSessionIdentityPort.ResolveLocalControlPlayerId() =>
            ResolveLocalControlPlayerId();

        MobaPlayerLoadout[] IBattleInputSessionIdentityPort.BuildEffectivePlayerLoadouts() =>
            BuildEffectivePlayerLoadouts();

        private readonly BattlePlayerLoadoutStore _playerLoadouts =
            new BattlePlayerLoadoutStore();

        public BattleLogicSession Session;
        public IWorld RuntimeWorld;
        public BattleStartPlan Plan;
        public int LastFrame;
        public double LogicTimeSeconds;
        public int LocalActorId;
        public string LocalControlPlayerId;
        public BattleSessionHooks Hooks;
        public bool CanSubmitGameplayInput = true;
        public int RuntimePlayerLoadoutRevision => _playerLoadouts.Revision;

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
            if (!_playerLoadouts.Apply(in entry, Plan.LaunchSpec.Players)) return;

            if (string.Equals(
                    ResolveLocalControlPlayerId(),
                    entry.PlayerId,
                    StringComparison.OrdinalIgnoreCase))
            {
                LocalActorId = entry.ActorId;
            }
        }

        public MobaPlayerLoadout[] BuildEffectivePlayerLoadouts() =>
            _playerLoadouts.BuildEffective(Plan.LaunchSpec.Players);

        private void ClearRuntimePlayerLoadouts() => _playerLoadouts.Clear();
    }
}
