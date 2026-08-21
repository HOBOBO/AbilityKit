using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Battle;
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
            var world = _plan.World;
            var gateway = _plan.Gateway;

            if (_plan.HostMode == BattleHostMode.GatewayRemote && gateway.UseGatewayTransport)
            {
                if (!uint.TryParse(world.PlayerId, out var localPlayerId))
                {
                    throw new InvalidOperationException($"GatewayRemote requires numeric PlayerId. playerId='{world.PlayerId}'");
                }

                var roomId = gateway.NumericRoomId;
                if (roomId == 0 && !ulong.TryParse(world.WorldId, out roomId))
                {
                    throw new InvalidOperationException($"GatewayRemote requires numeric WorldId(roomId). worldId='{world.WorldId}'");
                }

                if (_plan.Sync.SyncMode != BattleSyncMode.Lockstep)
                {
                    throw new InvalidOperationException(
                        $"MOBA GatewayRemote only supports Lockstep frame sync. SyncMode={_plan.Sync.SyncMode}");
                }

                var transport = _transportFactory.CreateGatewayRemoteTransport(
                    _plan,
                    localPlayerId,
                    roomId,
                    _unityDispatcher,
                    _networkIoDispatcher);
                if (transport is NetworkTransport networkTransport)
                {
                    _runtime.InputSubmissionDiagnostics.Bind(
                        networkTransport,
                        _plan.World.WorldId);
                }

                return _sessionRegistry.Start(opts, remoteTransport: transport);
            }

            return _sessionRegistry.Start(opts);
        }

        public MobaSynchronizationHealthSnapshot SynchronizationHealth =>
            _runtime.Diagnostics.SynchronizationHealth;

        public SyncHealthReport SynchronizationHealthReport =>
            _runtime.Diagnostics.SynchronizationHealthReport;

        private void TickRemoteInterpolation(float deltaTime)
        {
            _runtime.Replication.TickPresentation(
                _ctx,
                _plan.Authority.EnableClientPrediction,
                deltaTime);
        }
    }
}
