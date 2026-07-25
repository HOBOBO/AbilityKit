using System;
using System.Threading.Tasks;
using AbilityKit.Core.Logging;

namespace AbilityKit.Game.Battle.Agent
{
    /// <summary>
    /// 断线重连编排器——协调房间恢复 + 战斗追帧 + 哈希重置。
    ///
    /// 当前（P1 最小路径）：恢复房间元数据后，重新订阅 state sync 触发服务端 FullSnapshot。
    /// 服务端的 BattleSnapshotSyncPolicy.observerCount > 0 保证重连后立即推送最新帧数据。
    /// 客户端收到 FullSnapshot 后通过 RemoteInterpolationPlayback 缓冲插值恢复显示。
    ///
    /// 待补全（P2）：显式调用 RequestFullSnapshot + CatchUpToServerFrame 的追帧路径。
    /// </summary>
    public sealed class ReconnectOrchestrator
    {
        private readonly GatewayRoomClient _roomClient;
        private readonly string _sessionToken;
        private readonly string _region;
        private readonly string _serverId;
        private readonly string _battleId;
        private readonly Action _onReconnected;
        private readonly Action<string> _onReconnectFailed;

        private bool _isReconnecting;
        private int _retryCount;

        public ReconnectOrchestrator(
            GatewayRoomClient roomClient,
            string sessionToken,
            string region,
            string serverId,
            string battleId,
            Action onReconnected = null,
            Action<string> onReconnectFailed = null)
        {
            _roomClient = roomClient ?? throw new ArgumentNullException(nameof(roomClient));
            _sessionToken = sessionToken ?? throw new ArgumentNullException(nameof(sessionToken));
            _region = region ?? string.Empty;
            _serverId = serverId ?? string.Empty;
            _battleId = battleId ?? string.Empty;
            _onReconnected = onReconnected;
            _onReconnectFailed = onReconnectFailed;
        }

        public bool IsReconnecting => _isReconnecting;

        /// <summary>
        /// 触发重连流程。幂等——如果已在重连中直接返回。
        /// </summary>
        public async void TryReconnect()
        {
            if (_isReconnecting) return;
            _isReconnecting = true;

            try
            {
                var result = await _roomClient.RestoreRoomAsync(
                    _sessionToken, _region, _serverId);

                if (!result.Success)
                {
                    Log.Warning($"[ReconnectOrchestrator] RestoreRoom failed. success=false");
                    _onReconnectFailed?.Invoke("RestoreRoom returned success=false");
                    return;
                }

                if (!result.IsInBattle)
                {
                    Log.Warning($"[ReconnectOrchestrator] RestoreRoom returned IsInBattle=false, not in battle.");
                    _onReconnectFailed?.Invoke("Not in battle");
                    return;
                }

                // 重新订阅 state sync——服务端 detection 到新 observer 后
                // 触发 FullSnapshot 推送（BattleSnapshotSyncPolicy.ShouldPublish）。
                var subResult = await _roomClient.SubscribeStateSyncAsync(
                    _sessionToken, _battleId, result.RoomId);
                if (!subResult.Success)
                {
                    Log.Warning($"[ReconnectOrchestrator] SubscribeStateSync failed after restore.");
                    _onReconnectFailed?.Invoke("SubscribeStateSync failed");
                    return;
                }

                Log.Info($"[ReconnectOrchestrator] Reconnected. battleId={_battleId} roomId={result.RoomId}");
                _retryCount = 0;
                _onReconnected?.Invoke();
            }
            catch (Exception ex)
            {
                _retryCount++;
                Log.Exception(ex, $"[ReconnectOrchestrator] RestoreRoom exception. retryCount={_retryCount}");
                if (_retryCount < 3)
                {
                    // 简单重试（不退避——生产环境应加指数退避）
                    _isReconnecting = false;
                    TryReconnect();
                    return;
                }

                _onReconnectFailed?.Invoke($"RestoreRoom exception after {_retryCount} retries: {ex.Message}");
            }
            finally
            {
                _isReconnecting = false;
            }
        }
    }
}
