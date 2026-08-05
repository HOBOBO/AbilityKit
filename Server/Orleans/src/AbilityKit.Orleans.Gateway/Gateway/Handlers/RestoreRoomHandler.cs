using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Room;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

/// <summary>
/// 鎭㈠褰撳墠璐﹀彿鎵€鍦ㄦ埧闂?Handler銆?
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.RestoreRoom)]
public sealed partial class RestoreRoomHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly Core.GatewayRoomStatePushSubscriptionManager? _roomStatePushSubscriptions;

    public RestoreRoomHandler(
        IClusterClient clusterClient,
        Core.GatewayRoomStatePushSubscriptionManager? roomStatePushSubscriptions = null)
    {
        _clusterClient = clusterClient;
        _roomStatePushSubscriptions = roomStatePushSubscriptions;
    }

    public override async ValueTask<GatewayResponse> HandleAsync(
        GatewayRequest request,
        GatewaySessionContext context,
        CancellationToken cancellationToken)
    {
        if (request.Payload == null || request.Payload.Length == 0)
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        var req = WireRoomGatewayBinary.Deserialize<WireRestoreRoomReq>(request.Payload);
        if (string.IsNullOrWhiteSpace(req.SessionToken))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        try
        {
            var accountId = await RoomGatewayWireMapper.ValidateAccountAsync(_clusterClient, req.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

            var mapping = _clusterClient.GetGrain<IRoomIdMappingGrain>("global");
            var roomId = await mapping.TryGetAccountRoomAsync(accountId);
            if (string.IsNullOrWhiteSpace(roomId))
            {
                var empty = RoomGatewayWireMapper.ToEmptyRestoreRoomRes("No active room for account.");
                var emptyPayload = WireRoomGatewayBinary.Serialize(in empty);
                return GatewayResponse.Ok(request.Seq, emptyPayload.ToArray());
            }

            var room = _clusterClient.GetGrain<IRoomGrain>(roomId);
            var restore = await room.RestoreAsync(accountId);
            if (restore.HasActiveRoom)
            {
                await mapping.BindAccountRoomAsync(accountId, roomId);
            }
            else
            {
                await mapping.ClearAccountRoomAsync(accountId, roomId);
            }

            var wire = RoomGatewayWireMapper.ToRestoreRoomRes(restore, accountId);
            var responsePayload = WireRoomGatewayBinary.Serialize(in wire);

            context.RoomId = roomId;
            context.AccountId = accountId;
            if (restore.HasActiveRoom &&
                context.ConnectionId > 0 &&
                _roomStatePushSubscriptions != null)
            {
                await _roomStatePushSubscriptions.EnsureBoundAsync(
                    context.ConnectionId,
                    roomId,
                    accountId);
            }

            return GatewayResponse.Ok(request.Seq, responsePayload.ToArray());
        }
        catch (Exception exception)
        {
            return RoomGatewayErrorMapper.ToResponse(request.Seq, exception);
        }
    }
}

