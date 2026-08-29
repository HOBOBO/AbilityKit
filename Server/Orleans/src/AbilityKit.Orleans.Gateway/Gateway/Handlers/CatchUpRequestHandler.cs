using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

[Core.GatewayHandler(OpCodes.CatchUpRequest)]
public sealed partial class CatchUpRequestHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly IGatewaySessionRegistry _sessionRegistry;
    private readonly Core.GatewayFrameSyncSubscriptionManager _subscriptions;
    private readonly ILogger<CatchUpRequestHandler> _logger;

    public CatchUpRequestHandler(
        IClusterClient clusterClient,
        IGatewaySessionRegistry sessionRegistry,
        Core.GatewayFrameSyncSubscriptionManager subscriptions,
        ILogger<CatchUpRequestHandler> logger)
    {
        _clusterClient = clusterClient;
        _sessionRegistry = sessionRegistry;
        _subscriptions = subscriptions;
        _logger = logger;
    }

    public override async ValueTask<GatewayResponse> HandleAsync(
        GatewayRequest request,
        GatewaySessionContext context,
        CancellationToken cancellationToken)
    {
        if (request.Payload == null || request.Payload.Length == 0)
        {
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(context.AccountId)
            || string.IsNullOrWhiteSpace(context.SessionToken))
        {
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.Unauthorized);
        }

        WireCatchUpRequest wireRequest;
        try
        {
            wireRequest = WireCustomBinary.DeserializeCatchUpRequest(request.Payload);
        }
        catch (Exception)
        {
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
        }

        if (wireRequest.RoomId == 0 || wireRequest.WorldId == 0)
        {
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
        }

        try
        {
            var mapping = _clusterClient.GetGrain<IRoomIdMappingGrain>("global");
            var roomId = await mapping.TryGetRoomIdAsync(wireRequest.RoomId);
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.NotFound);
            }

            var accountRoomId = await mapping.TryGetAccountRoomAsync(context.AccountId);
            if (!string.Equals(accountRoomId, roomId, StringComparison.Ordinal))
            {
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.Forbidden);
            }

            var room = _clusterClient.GetGrain<IRoomGrain>(roomId);
            var snapshot = await room.GetSnapshotAsync();
            if (snapshot.Phase != RoomPhase.InBattle)
            {
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.Forbidden);
            }

            await _subscriptions.EnsureSubscribedAsync(context.ConnectionId, wireRequest.RoomId);

            var frameSync = _clusterClient.GetGrain<IBattleFrameSyncGrain>(wireRequest.RoomId.ToString());
            var payload = await frameSync.RequestCatchUpAsync(
                new FrameSyncCatchUpRequest(
                    wireRequest.RoomId,
                    wireRequest.WorldId,
                    wireRequest.FromFrameExclusive,
                    wireRequest.ToFrameInclusive));

            if (payload is null)
            {
                // MOBA 使用锁步帧同步；权威输入历史不连续或请求范围无效时明确拒绝追帧恢复，
                // 不得回退到 shooter 状态同步使用的全量状态快照。
                _logger.LogWarning(
                    "[CatchUpRequestHandler] Frame-sync catch-up rejected. ConnectionId={ConnectionId} AccountId={AccountId} RoomId={RoomId} WorldId={WorldId} FromFrameExclusive={FromFrameExclusive} ToFrameInclusive={ToFrameInclusive}",
                    context.ConnectionId,
                    context.AccountId,
                    wireRequest.RoomId,
                    wireRequest.WorldId,
                    wireRequest.FromFrameExclusive,
                    wireRequest.ToFrameInclusive);
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.NotFound);
            }

            // 转换 Orleans payload → wire payload 并通过 push 推送给客户端
            var wireFrames = new WireCatchUpFrame[payload.FrameInputs.Count];
            for (var i = 0; i < payload.FrameInputs.Count; i++)
            {
                var frameInputs = payload.FrameInputs[i];
                var wireInputs = new WireInputItem[frameInputs.Count];
                for (var j = 0; j < frameInputs.Count; j++)
                {
                    var fi = frameInputs[j];
                    wireInputs[j] = new WireInputItem(fi.PlayerId, fi.OpCode, fi.Payload);
                }

                wireFrames[i] = new WireCatchUpFrame(payload.StartFrame + i, wireInputs);
            }

            var wirePush = new WireCatchUpPayloadPush(
                payload.RoomId,
                payload.WorldId,
                payload.StartFrame,
                wireFrames);

            var pushPayload = WireCustomBinary.Serialize(wirePush);
            if (_sessionRegistry.TryGetSession(context.ConnectionId, out var session) && session is not null)
            {
                await session.SendServerPushAsync(
                    OpCodes.CatchUpPayloadPush,
                    pushPayload.Array ?? Array.Empty<byte>(),
                    cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "[CatchUpRequestHandler] Transport session not found for connection. ConnectionId: {ConnectionId}",
                    context.ConnectionId);
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.InternalError);
            }

            return GatewayResponse.Ok(request.Seq);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CatchUpRequestHandler] Error handling CatchUp request");
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.InternalError);
        }
    }
}
