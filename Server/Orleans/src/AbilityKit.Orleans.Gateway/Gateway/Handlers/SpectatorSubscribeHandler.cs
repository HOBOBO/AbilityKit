using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using Microsoft.Extensions.Logging;
using MemoryPack;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

/// <summary>
/// 观战订阅处理器。观战者加入房间的帧同步广播，仅接收 FramePushed 事件，不参与输入提交。
/// 输入提交由 <see cref="SubmitFrameInputHandler"/> 的房间成员身份检查拦截。
/// </summary>
[Core.GatewayHandler(OpCodes.SpectatorSubscribe)]
public sealed partial class SpectatorSubscribeHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly Core.GatewayFrameSyncSubscriptionManager _subscriptions;
    private readonly ILogger<SpectatorSubscribeHandler> _logger;

    public SpectatorSubscribeHandler(
        IClusterClient clusterClient,
        Core.GatewayFrameSyncSubscriptionManager subscriptions,
        ILogger<SpectatorSubscribeHandler> logger)
    {
        _clusterClient = clusterClient;
        _subscriptions = subscriptions;
        _logger = logger;
    }

    public override async ValueTask<GatewayResponse> HandleAsync(
        GatewayRequest request,
        GatewaySessionContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.AccountId)
            || string.IsNullOrWhiteSpace(context.SessionToken))
        {
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.Unauthorized);
        }

        // Payload: roomId (ulong, 8 bytes)，哨兵值 0 表示从 account room 推断
        ulong roomId = 0;
        if (request.Payload is { Length: >= 8 })
        {
            roomId = BitConverter.ToUInt64(request.Payload, 0);
        }

        try
        {
            if (roomId == 0)
            {
                var mapping = _clusterClient.GetGrain<IRoomIdMappingGrain>("global");
                var accountRoom = await mapping.TryGetAccountRoomAsync(context.AccountId);
                if (string.IsNullOrWhiteSpace(accountRoom) || !ulong.TryParse(accountRoom, out roomId))
                {
                    return GatewayResponse.Error(request.Seq, GatewayStatusCode.NotFound);
                }
            }

            // 验证房间存在且处于战斗中
            var roomIdStr = roomId.ToString();
            var room = _clusterClient.GetGrain<IRoomGrain>(roomIdStr);
            var snapshot = await room.GetSnapshotAsync();
            if (snapshot.Phase != RoomPhase.InBattle)
            {
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.Forbidden);
            }

            // 注册为帧同步观察者（仅接收广播，不提交输入）
            await _subscriptions.EnsureSubscribedAsync(context.ConnectionId, roomId);

            _logger.LogInformation(
                "[SpectatorSubscribeHandler] Spectator subscribed. ConnectionId={ConnectionId} RoomId={RoomId} AccountId={AccountId}",
                context.ConnectionId, roomId, context.AccountId);

            // 返回成功 + 房间基础信息（WorldId、TickRate）供观战客户端初始化世界
            var frameSync = _clusterClient.GetGrain<IBattleFrameSyncGrain>(roomIdStr);
            var metrics = await frameSync.GetMetricsAsync();

            var responseWire = new WireSpectatorSubscribeRes(
                metrics.WorldId,
                metrics.TickRate,
                metrics.CurrentFrame);

            var responsePayload = MemoryPackSerializer.Serialize(responseWire);
            return GatewayResponse.Ok(request.Seq, responsePayload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SpectatorSubscribeHandler] Error subscribing spectator. RoomId={RoomId}", roomId);
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.InternalError);
        }
    }
}
