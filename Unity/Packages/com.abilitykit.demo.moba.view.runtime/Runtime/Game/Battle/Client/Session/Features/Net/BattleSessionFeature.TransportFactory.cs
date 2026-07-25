using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle.Transport;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private MobaRemoteInterpolationPlayback _remoteInterpolationPlayback;
        private NetworkTransport _interpolationTransport;
        private int _lastServerAckFrame;
        private bool _pendingStateImport;

        private BattleLogicSession StartBattleLogicSession(BattleLogicSessionOptions opts)
        {
            var world = _plan.World;
            var gateway = _plan.Gateway;

            if (_plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && gateway.UseGatewayTransport)
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

                // 远端实体插值播放：Gateway 推送 SnapshotPushed → 缓冲 → 每帧投影
                if (transport is NetworkTransport networkTransport)
                {
                    _remoteInterpolationPlayback = new MobaRemoteInterpolationPlayback();
                    _interpolationTransport = networkTransport;
                    networkTransport.StateSyncSnapshotPushed += OnStateSyncSnapshotPushed;
                    BattleSyncFeature.EnableRemoteInterpolation = true;

                    // 输入 ACK 帧回传：SubmitInput 的 response 携带服务端帧号，
                    // 用于 RemoteDrivenWorldTickDriver 诊断预测窗口偏差。
                    networkTransport.Options.OnSubmitInputAck = serverFrame =>
                    {
                        _lastServerAckFrame = serverFrame;
                    };

                    // 断线重连：断线检测 → 退避重连 → 状态重置追帧
                    HookReconnectWatch(networkTransport);
                }

                return BattleLogicSessionHost.Start(opts, remoteTransport: transport);
            }

            return BattleLogicSessionHost.Start(opts);
        }

        private void OnStateSyncSnapshotPushed(object rawSnapshot)
        {
            if (rawSnapshot is not GatewayStateSyncSnapshot snapshot) return;

            // 重连恢复：首个 FullSnapshot 重建预测世界并导入服务端状态。
            if (_pendingStateImport && snapshot.IsFullSnapshot)
            {
                TryImportStateIntoLogicWorld(in snapshot);
            }

            _remoteInterpolationPlayback?.Observe(in snapshot);
        }

        /// <summary>
        /// 把 FullSnapshot 导入重建后的预测世界：
        /// 重建世界 → 解析 MobaLogicWorldStateImporter → 导入 actor 状态 → 对齐帧号。
        /// 导入成功后预测驱动与哈希对账从该帧恢复。
        /// </summary>
        private void TryImportStateIntoLogicWorld(in GatewayStateSyncSnapshot snapshot)
        {
            if (_ctx == null || _handles.Session == null) return;

            // 重建预测世界（EnsureStarted 幂等——世界在 ResetStateAfterReconnect 已销毁）
            StartRemoteDrivenLocalWorld();

            var world = _handles.RemoteDriven.World;
            if (world?.Services == null)
            {
                Log.Warning("[BattleSessionFeature] State import skipped: RemoteDriven world unavailable after recreate.");
                return;
            }

            if (!world.Services.TryResolve<AbilityKit.Demo.Moba.Services.StateImport.MobaLogicWorldStateImporter>(out var importer) || importer == null)
            {
                Log.Warning("[BattleSessionFeature] State import skipped: MobaLogicWorldStateImporter not registered in world services.");
                return;
            }

            var actors = snapshot.Actors;
            if (actors != null && actors.Length > 0)
            {
                var imports = new AbilityKit.Demo.Moba.Services.StateImport.MobaActorStateImport[actors.Length];
                for (int i = 0; i < actors.Length; i++)
                {
                    var a = actors[i];
                    imports[i] = new AbilityKit.Demo.Moba.Services.StateImport.MobaActorStateImport(
                        a.ActorId, a.X, a.Y, a.Z, a.Rotation, a.Hp, a.HpMax, a.TeamId, a.Kind, a.Code, a.OwnerNetId);
                }

                var result = importer.Import(imports, snapshot.Frame, isFullSnapshot: true);
                Log.Info($"[BattleSessionFeature] State import done. frame={snapshot.Frame} {result}");
            }

            // 帧号对齐：世界从快照帧继续推进
            _remoteDrivenLastTickedFrame = snapshot.Frame;
            _pendingStateImport = false;
        }

        private void TickRemoteInterpolation(float deltaTime)
        {
            TickReconnect(deltaTime);

            if (_remoteInterpolationPlayback == null || _ctx == null) return;

            _remoteInterpolationPlayback.Advance(deltaTime);

            if (_remoteInterpolationPlayback.TryProjectRemoteFrame(out var projected))
            {
                // 预测世界存在时本地玩家由 PredictionViewBridge 驱动（插值跳过）；
                // 不存在时（如断线重连降级后）本地玩家也交给插值驱动。
                var localActorId = _handles.RemoteDriven.World != null ? _ctx.LocalActorId : 0;
                BattleRemoteInterpolationApplier.Apply(_ctx, in projected, localActorId);
            }
        }

        private void DisposeRemoteInterpolation()
        {
            UnhookReconnectWatch();

            if (_interpolationTransport != null)
            {
                _interpolationTransport.StateSyncSnapshotPushed -= OnStateSyncSnapshotPushed;
                _interpolationTransport = null;
            }

            _remoteInterpolationPlayback?.Reset();
            _remoteInterpolationPlayback = null;
            _pendingStateImport = false;
            BattleSyncFeature.EnableRemoteInterpolation = false;
        }
    }
}
