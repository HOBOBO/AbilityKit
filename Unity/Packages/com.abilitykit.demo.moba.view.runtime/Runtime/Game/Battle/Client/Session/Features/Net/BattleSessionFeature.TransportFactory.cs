using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Core.Logging;
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

                var transport = _transportFactory.CreateGatewayRemoteTransport(
                    _plan,
                    localPlayerId,
                    roomId,
                    _unityDispatcher,
                    _networkIoDispatcher);

                // 远端实体插值播放：Gateway 推送 SnapshotPushed → 统一复制管线 → 每帧投影。
                if (transport is NetworkTransport networkTransport)
                {
                    var reliableEventCheckpoint = _plan.ReliableEventCheckpoint;
                    var checkpointAccepted = _runtime.Replication.Build(
                        networkTransport,
                        _plan.World.TickRate,
                        roomId,
                        gateway.BattleId ?? string.Empty,
                        in reliableEventCheckpoint,
                        _runtime.Recovery.HandleSnapshot,
                        _runtime.Recovery.HandleReliableEvents,
                        _runtime.Recovery.HandleConnectionClosed,
                        _runtime.Recovery.HandleConnectionEstablished,
                        _runtime.Recovery.HandleAuthenticationFailed,
                        _plan.RemoteSyncCapabilities);
                    if (!checkpointAccepted)
                    {
                        Log.Warning(
                            "[BattleSessionFeature] Reliable event checkpoint rejected " +
                            "because it does not match the active battle.");
                    }

                    var recoveryPlan = _plan;

                    _runtime.Recovery.BeginGeneration(
                        new NetworkBattleRecoveryTransportOperations(networkTransport),
                        _bootstrapper as IMobaReliableBattleEventCheckpointStore,
                        _ctx,
                        in recoveryPlan,
                        GetFixedDeltaSeconds(),
                        ResolveIdealFrameLimit,
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        () => _runtime.Diagnostics.ShouldForceClientHashMismatch,
#else
                        () => false,
#endif
                        value => ReliableBattleEventReceived?.Invoke(value),
                        NotifyFirstFrameReceivedOnce);
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
            _runtime.Replication.TickPresentation(_ctx, _handles, deltaTime);
        }

        private void DisposeRemoteInterpolation()
        {
            _runtime.DisposeReplication();
            if (_ctx != null)
            {
                _ctx.CanSubmitGameplayInput = true;
            }
        }
    }
}
