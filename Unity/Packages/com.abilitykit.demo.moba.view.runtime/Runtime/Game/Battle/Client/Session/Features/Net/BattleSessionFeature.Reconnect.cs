using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Battle;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 断线后的战斗状态恢复（客户端战斗链路）。
    ///
    /// 触发链：
    /// ConnectionManager 统一执行退避重连 → NetworkTransport.ConnectionEstablished
    /// → 重置客户端状态 → 世界随 FullSnapshot 追帧恢复。
    /// 本 Feature 只消费连接事件，不维护第二套 socket 重连计时或 attempt 生命周期。
    ///
    /// 重连后的状态重置（P1 端到端）：
    /// 1. 销毁 RemoteDriven 预测世界（含重置 Simulation owner 的 last-ticked frame），标记待状态导入
    /// 2. 重置远端插值缓冲（MobaRemoteInterpolationPlayback.Reset）
    /// 3. 重置状态哈希对账（PredictionReconcileControl.ResetReconcile）
    /// 4. 清空输入 ACK 帧跟踪
    /// 5. 首个 FullSnapshot 到达时：重建预测世界 + MobaLogicWorldStateImporter 导入状态
    ///    + 帧号对齐，预测驱动与哈希对账恢复（期间插值层临时驱动全部 actor）
    ///
    /// 服务端配合（已就绪）：
    /// 重连后 NetworkTransport 自动 RenewSession + SubscribeStateSync（PostAuthentication），
    /// BattleLogicHostGrain.SubscribeAsync 对新 observer 立即 PushSnapshotTo(isFullSnapshot: true)。
    /// </summary>
    public sealed partial class BattleSessionFeature
    {
        private bool _battleConnectionRecoveryPending;

        private void OnBattleConnectionClosed()
        {
            // 会话已停止（主动 Disconnect）时不进入自动重连。
            if (!_runtime.Replication.IsBuilt || _handles.Session == null) return;
            _battleConnectionRecoveryPending = true;

            Log.Warning(
                "[BattleSessionFeature] Battle connection lost. " +
                "ConnectionManager is scheduling transport recovery.");
        }

        private void OnBattleConnectionEstablished()
        {
            if (!_runtime.Replication.IsBuilt || !_battleConnectionRecoveryPending) return;

            _battleConnectionRecoveryPending = false;

            Log.Info("[BattleSessionFeature] Battle connection re-established. Resetting client state for catch-up.");
            ResetStateAfterReconnect();
        }

        private void ResetStateAfterReconnect()
        {
            // 1. 销毁预测世界（内部已重置 Simulation owner 的 last-ticked frame）。
            //
            // 世界在首个 FullSnapshot 到达时重建并导入服务端状态
            // （见 TransportFactory.TryImportStateIntoLogicWorld）。
            // 在此之前的窗口期内，插值层驱动全部 actor（含本地玩家），画面保持正确。
            DisposeRemoteDrivenWorld();
            _pendingStateImport = true;
            if (_ctx != null) _ctx.CanSubmitGameplayInput = false;
            _snapshotAdmission?.RequireFullBaseline();
            _authoritativeSnapshotState?.Reset();

            // 2. 重置远端插值缓冲——旧快照时间线已失效
            _remoteInterpolationController?.Reset();

            // 3. 重置状态哈希对账——避免旧哈希历史在重连后立即误报 mismatch
            var reconcileControl = _ctx?.PredictionReconcileControl;
            if (reconcileControl != null)
            {
                WorldId worldId;
                if (_ctx.HasRuntimeWorldId)
                {
                    worldId = _ctx.RuntimeWorldId;
                    reconcileControl.ResetReconcile(worldId);
                }
                else if (!string.IsNullOrWhiteSpace(_plan.World.WorldId))
                {
                    worldId = new WorldId(_plan.World.WorldId);
                    reconcileControl.ResetReconcile(worldId);
                }
                else
                {
                    worldId = default;
                }

                // 重连后重新启用对账——避免因 replay 超时（120 tick）导致 ReconcileEnabled
                // 被永久禁用，使得后续静默状态分歧无法检测。
                if (!string.IsNullOrWhiteSpace(worldId.Value))
                {
                    reconcileControl.SetReconcileEnabled(worldId, true);
                }
            }

            // 4. 清空输入 ACK 帧跟踪
            _lastServerAckFrame = 0;
        }

        /// <summary>
        /// 鉴权/订阅握手失败（RenewSession 或 SubscribeStateSync 异常）。TCP 已连但推送流未建立 ——
        /// 如果不处理，会话会卡在等 baseline 的僵尸态。处置：断开当前连接，让 ConnectionManager
        /// 进入退避重连周期，下次建连引擎自动重试 RenewSession。token 真过期时最终触发重连耗尽。
        /// </summary>
        private void OnBattleAuthenticationFailed(Exception ex)
        {
            if (!_runtime.Replication.IsBuilt) return;

            Log.Warning(
                $"[BattleSessionFeature] Battle authentication failed: {ex?.Message}. " +
                "Disconnecting to trigger reconnect + re-auth cycle.");

            _battleConnectionRecoveryPending = true;
            try
            {
                _runtime.Replication.Transport?.Disconnect();
            }
            catch (Exception disconnectEx)
            {
                Log.Exception(disconnectEx, "[BattleSessionFeature] Transport disconnect after auth failure failed.");
            }
        }
    }
}
