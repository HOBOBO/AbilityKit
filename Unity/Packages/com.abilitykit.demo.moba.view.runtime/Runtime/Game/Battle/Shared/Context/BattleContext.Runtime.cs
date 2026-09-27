using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Share.Prediction;
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
        private readonly Dictionary<int, int> _actorEntityVersions = new Dictionary<int, int>();
        private MobaActionAckTracker _localActionAckTracker;

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
        public int ActionInterruptEpoch => _localActionAckTracker?.LatestInterruptEpoch ?? 0;
        public long AcceptedActionAckCount => _localActionAckTracker?.AcceptedCount ?? 0L;
        public long RejectedActionAckCount => _localActionAckTracker?.RejectedCount ?? 0L;
        public long StaleActionAckCount => _localActionAckTracker?.StaleCount ?? 0L;

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
                EnsureLocalActionAckTracker();
            }
        }

        public void ObserveActorSpawnIdentity(int actorId, int entityVersion)
        {
            if (actorId <= 0) return;
            _actorEntityVersions[actorId] = entityVersion > 0 ? entityVersion : 1;
            if (actorId == LocalActorId) EnsureLocalActionAckTracker();
        }

        public void ForgetActorIdentity(int actorId)
        {
            if (actorId <= 0) return;
            _actorEntityVersions.Remove(actorId);
            if (actorId == LocalActorId) _localActionAckTracker = null;
        }

        public int GetActorEntityVersion(int actorId)
        {
            return actorId > 0 && _actorEntityVersions.TryGetValue(actorId, out var version)
                ? version
                : 0;
        }

        public void ApplyActionAcks(MobaActionAckEntry[] entries)
        {
            if (entries == null || entries.Length == 0 || LocalActorId <= 0) return;
            EnsureLocalActionAckTracker();
            if (_localActionAckTracker == null) return;

            for (int i = 0; i < entries.Length; i++)
            {
                _localActionAckTracker.Apply(in entries[i]);
            }
        }

        public MobaPlayerLoadout[] BuildEffectivePlayerLoadouts() =>
            _playerLoadouts.BuildEffective(Plan.LaunchSpec.Players);

        private void ClearRuntimePlayerLoadouts()
        {
            _playerLoadouts.Clear();
            _actorEntityVersions.Clear();
            _localActionAckTracker = null;
        }

        private void EnsureLocalActionAckTracker()
        {
            var version = GetActorEntityVersion(LocalActorId);
            if (LocalActorId <= 0 || version <= 0) return;
            if (_localActionAckTracker != null &&
                _localActionAckTracker.ActorId == LocalActorId &&
                _localActionAckTracker.EntityVersion == version)
            {
                return;
            }

            _localActionAckTracker = new MobaActionAckTracker(LocalActorId, version);
        }
    }
}
