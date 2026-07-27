using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle.Transport;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 断线重连编排（客户端战斗链路）。
    ///
    /// 触发链：
    /// NetworkTransport.ConnectionClosed → 进入重连等待（指数退避）→ Connect()
    /// → ConnectionEstablished → 重置客户端状态 → 世界随 FullSnapshot 追帧恢复。
    ///
    /// 重连后的状态重置（P1 端到端）：
    /// 1. 销毁 RemoteDriven 预测世界（含重置 _remoteDrivenLastTickedFrame），标记待状态导入
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
        private const int MaxReconnectAttempts = ReconnectBackoffPolicy.MaxAttempts;

        private bool _reconnectWatchEnabled;
        private bool _reconnectPending;
        private int _reconnectAttempts;
        private float _reconnectTimer;

        private void HookReconnectWatch(NetworkTransport transport)
        {
            if (transport == null || _reconnectWatchEnabled) return;

            _reconnectWatchEnabled = true;
            _reconnectPending = false;
            _reconnectAttempts = 0;
            _reconnectTimer = 0f;
            transport.ConnectionClosed += OnBattleConnectionClosed;
            transport.ConnectionEstablished += OnBattleConnectionEstablished;
        }

        private void UnhookReconnectWatch()
        {
            if (!_reconnectWatchEnabled) return;
            _reconnectWatchEnabled = false;
            _reconnectPending = false;

            if (_interpolationTransport != null)
            {
                _interpolationTransport.ConnectionClosed -= OnBattleConnectionClosed;
                _interpolationTransport.ConnectionEstablished -= OnBattleConnectionEstablished;
            }
        }

        private void OnBattleConnectionClosed()
        {
            // 会话已停止（主动 Disconnect）时不进入自动重连。
            if (!_reconnectWatchEnabled || _handles.Session == null) return;
            if (_reconnectAttempts >= MaxReconnectAttempts) return;

            _reconnectPending = true;
            _reconnectTimer = 0f;
            Log.Warning($"[BattleSessionFeature] Battle connection lost. Scheduling reconnect (attempt {_reconnectAttempts + 1}/{MaxReconnectAttempts}).");
        }

        private void OnBattleConnectionEstablished()
        {
            if (!_reconnectWatchEnabled || !_reconnectPending) return;

            _reconnectPending = false;
            _reconnectAttempts = 0;
            _reconnectTimer = 0f;

            Log.Info("[BattleSessionFeature] Battle connection re-established. Resetting client state for catch-up.");
            ResetStateAfterReconnect();
        }

        private void TickReconnect(float deltaTime)
        {
            if (!_reconnectPending) return;

            _reconnectTimer += deltaTime;
            var delay = ReconnectBackoffPolicy.ResolveDelay(_reconnectAttempts);
            if (_reconnectTimer < delay) return;

            _reconnectTimer = 0f;
            _reconnectAttempts++;

            if (_reconnectAttempts > MaxReconnectAttempts)
            {
                _reconnectPending = false;
                Log.Error($"[BattleSessionFeature] Reconnect gave up after {MaxReconnectAttempts} attempts.");
                return;
            }

            Log.Info($"[BattleSessionFeature] Reconnect attempt {_reconnectAttempts}/{MaxReconnectAttempts}...");
            try
            {
                _interpolationTransport?.Connect();
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] Reconnect attempt failed");
            }
        }

        private void ResetStateAfterReconnect()
        {
            // 1. 销毁预测世界（内部已重置 _remoteDrivenLastTickedFrame = 0）。
            //
            // 世界在首个 FullSnapshot 到达时重建并导入服务端状态
            // （见 TransportFactory.TryImportStateIntoLogicWorld）。
            // 在此之前的窗口期内，插值层驱动全部 actor（含本地玩家），画面保持正确。
            DisposeRemoteDrivenWorld();
            _pendingStateImport = true;
            _snapshotAdmission?.RequireFullBaseline();
            _authoritativeSnapshotState?.Reset();

            // 2. 重置远端插值缓冲——旧快照时间线已失效
            _remoteInterpolationController?.Reset();

            // 3. 重置状态哈希对账——避免旧哈希历史在重连后立即误报 mismatch
            var reconcileControl = _ctx?.PredictionReconcileControl;
            if (reconcileControl != null)
            {
                if (_ctx.HasRuntimeWorldId)
                {
                    reconcileControl.ResetReconcile(_ctx.RuntimeWorldId);
                }
                else if (!string.IsNullOrWhiteSpace(_plan.World.WorldId))
                {
                    reconcileControl.ResetReconcile(new AbilityKit.Ability.World.Abstractions.WorldId(_plan.World.WorldId));
                }
            }

            // 4. 清空输入 ACK 帧跟踪
            _lastServerAckFrame = 0;
        }
    }
}
