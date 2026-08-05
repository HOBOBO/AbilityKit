using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Room;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

/// <summary>
/// 鍔犲叆鎴块棿 Handler
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.JoinRoom)]
public sealed partial class JoinRoomHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly Core.GatewayRoomStatePushSubscriptionManager? _roomStatePushSubscriptions;

    public JoinRoomHandler(
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

        var req = WireRoomGatewayBinary.Deserialize<WireJoinRoomReq>(request.Payload);
        if (string.IsNullOrWhiteSpace(req.SessionToken) || string.IsNullOrWhiteSpace(req.RoomId))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        try
        {
            var accountId = await RoomGatewayWireMapper.ValidateAccountAsync(_clusterClient, req.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

            var room = _clusterClient.GetGrain<IRoomGrain>(req.RoomId);
            var join = await room.JoinAsync(accountId);

            var mapping = _clusterClient.GetGrain<IRoomIdMappingGrain>("global");
            await mapping.BindAccountRoomAsync(accountId, req.RoomId);

            var wire = RoomGatewayWireMapper.ToJoinRoomRes(join, accountId);
            var responsePayload = WireRoomGatewayBinary.Serialize(in wire);

            context.RoomId = req.RoomId;
            context.AccountId = accountId;
            if (context.ConnectionId > 0 && _roomStatePushSubscriptions != null)
            {
                await _roomStatePushSubscriptions.EnsureBoundAsync(
                    context.ConnectionId,
                    req.RoomId,
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

