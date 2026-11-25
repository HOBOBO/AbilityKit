using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private int _lastServerAckFrame
        {
            get => _runtime.Replication.LastServerAckFrame;
            set => _runtime.Replication.LastServerAckFrame = value;
        }

        private BattleLogicSession StartBattleLogicSession(BattleLogicSessionOptions opts)
        {
            _ctx.SettledFrames = _plan.UsesSettledFrames
                ? new MobaSettledFrameSession(_plan)
                : null;
            var session = _connectionRuntime.Start(
                _plan, opts, _unityDispatcher, _networkIoDispatcher, _ctx.SettledFrames);
            if (!_plan.UsesStateSyncAuthority || session.NetworkTransport is not { } transport) return session;

            try
            {
                _runtime.Recovery.AttachStateSyncTransport(transport,
                    DefaultBattleSessionTransportFactory.ParseWorldId(new WorldId(_plan.World.WorldId)),
                    _bootstrapper as IMobaReliableBattleEventCheckpointStore, _ctx, _plan,
                    GetFixedDeltaSeconds(), value => ReliableBattleEventReceived?.Invoke(value),
                    NotifyFirstFrameReceivedOnce, session);
                return session;
            }
            catch
            {
                try { session.Dispose(); }
                catch (Exception cleanup) { Log.Exception(cleanup, "StateSync session startup cleanup failed."); }
                throw;
            }
        }

        public MobaSynchronizationHealthSnapshot SynchronizationHealth =>
            _runtime.Diagnostics.SynchronizationHealth;

        public SyncHealthReport SynchronizationHealthReport =>
            _runtime.Diagnostics.SynchronizationHealthReport;

        private void TickRemoteInterpolation(float deltaTime)
        {
            _runtime.Replication.TickPresentation(
                _ctx,
                deltaTime,
                snapshot => _clientDriver.PresentInterpolatedSnapshot(in snapshot));
        }
    }
}
