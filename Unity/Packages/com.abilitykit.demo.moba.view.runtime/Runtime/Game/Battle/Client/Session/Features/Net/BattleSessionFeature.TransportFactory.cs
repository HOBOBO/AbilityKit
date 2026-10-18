using AbilityKit.Ability.Host;
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
            return _connectionRuntime.Start(
                _plan, opts, _unityDispatcher, _networkIoDispatcher);
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
