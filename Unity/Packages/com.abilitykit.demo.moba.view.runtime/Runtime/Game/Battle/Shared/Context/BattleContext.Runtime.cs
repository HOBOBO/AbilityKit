using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Share.Prediction;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Game.Flow
{
    [Flags]
    internal enum BattleGameplayInputBlockReason
    {
        None = 0,
        PauseRecovery = 1,
        AuthoritativeRecovery = 2,
    }

    public sealed partial class BattleContext
    {
        int IBattleHudPresentationPort.LocalActorId
        {
            get => LocalActorId;
            set => LocalActorId = value;
        }

        AbilityKit.Ability.Host.Extensions.Moba.CreateWorld.MobaBattleLaunchSpec
            IBattleGameplayPresentationSource.LaunchSpec => Plan.LaunchSpec;
        BattleHostMode IBattleInputSessionIdentityPort.HostMode => Plan.HostMode;

        string IBattleInputSessionIdentityPort.ResolveLocalControlPlayerId() =>
            ResolveLocalControlPlayerId();

        MobaPlayerLoadout[] IBattleInputSessionIdentityPort.BuildEffectivePlayerLoadouts() =>
            BuildEffectivePlayerLoadouts();

        private readonly BattlePlayerLoadoutStore _playerLoadouts =
            new BattlePlayerLoadoutStore();
        private readonly Dictionary<int, int> _actorEntityVersions = new Dictionary<int, int>();
        private readonly Dictionary<int, CharacterActionData> _characterActions =
            new Dictionary<int, CharacterActionData>();
        private MobaActionAckTracker _localActionAckTracker;
        private BattleGameplayInputBlockReason _inputBlockReasons;

        public BattleLogicSession Session { get; internal set; }
        internal IBattleTriggerViewEventSource TriggerViewEventSource { get; set; }
        public IWorld RuntimeWorld { get; private set; }
        public BattleStartPlan Plan { get; internal set; }
        public int LastFrame { get; internal set; }
        public double LogicTimeSeconds { get; internal set; }
        public int LocalActorId { get; internal set; }
        public string LocalControlPlayerId { get; internal set; }
        public BattleSessionHooks Hooks { get; private set; }
        public bool CanSubmitGameplayInput => _inputBlockReasons == BattleGameplayInputBlockReason.None;

        internal void BindRuntimeSession(BattleLogicSession session,
            IBattleTriggerViewEventSource triggerViewEventSource, IWorld world)
        {
            Session = session;
            TriggerViewEventSource = triggerViewEventSource;
            RuntimeWorld = world;
        }

        internal void BindSessionHooks(BattleSessionHooks hooks) => Hooks = hooks;

        internal void ClearRuntimeSession()
        {
            Session = null;
            TriggerViewEventSource = null;
            RuntimeWorld = null;
            Hooks = null;
        }

        internal void SetGameplayInputBlocked(BattleGameplayInputBlockReason reason, bool blocked)
        {
            if (blocked) _inputBlockReasons |= reason;
            else _inputBlockReasons &= ~reason;
        }
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
                ApplyLocalActorIdentity(entry.ActorId);
                EnsureLocalActionAckTracker();
            }
        }

        private int _snapshotLocalActorId;

        public void ApplyLocalActorIdentity(int actorId)
        {
            if (actorId <= 0) return;
            _snapshotLocalActorId = actorId;
            LocalActorId = actorId;
            EnsureLocalActionAckTracker();
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
            if (_snapshotLocalActorId == actorId) _snapshotLocalActorId = 0;
            _characterActions.Remove(actorId);
            if (actorId == LocalActorId) _localActionAckTracker = null;
        }

        public int GetActorEntityVersion(int actorId)
        {
            return actorId > 0 && _actorEntityVersions.TryGetValue(actorId, out var version)
                ? version
                : 0;
        }

        internal void ApplyCharacterActions(CharacterActionData[] entries)
        {
            if (entries == null) return;
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.ActorId <= 0 || entry.EntityVersion <= 0 ||
                    (_characterActions.TryGetValue(entry.ActorId, out var existing) &&
                     (entry.EntityVersion < existing.EntityVersion ||
                      (entry.EntityVersion == existing.EntityVersion &&
                       entry.SnapshotFrame <= existing.SnapshotFrame)))) continue;
                _characterActions[entry.ActorId] = entry;
            }
        }

        bool IBattleCharacterActionReadPort.TryGetCharacterAction(int actorId, out CharacterActionData action)
        {
            if (_characterActions.TryGetValue(actorId, out action) &&
                GetActorEntityVersion(actorId) == action.EntityVersion) return true;
            action = default;
            return false;
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
