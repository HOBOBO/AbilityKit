using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Orleans.Gateway.Core;
using AbilityKit.Protocol.Room;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

/// <summary>
/// Removes the authenticated account from the authoritative room membership.
/// RoomGrain owns owner transfer, loading rollback, room closing, and mapping cleanup.
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.LeaveRoom)]
public sealed partial class LeaveRoomHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly GatewayRoomMembershipService _roomMembership;

    public LeaveRoomHandler(
        IClusterClient clusterClient,
        GatewayRoomMembershipService roomMembership)
    {
        _clusterClient = clusterClient;
        _roomMembership = roomMembership;
    }

    public override async ValueTask<GatewayResponse> HandleAsync(
        GatewayRequest request,
        GatewaySessionContext context,
        CancellationToken cancellationToken)
    {
        if (request.Payload == null || request.Payload.Length == 0)
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        var req = WireRoomGatewayBinary.Deserialize<WireLeaveRoomReq>(request.Payload);
        var roomId = string.IsNullOrWhiteSpace(req.RoomId) ? context.RoomId : req.RoomId;
        if (string.IsNullOrWhiteSpace(req.SessionToken) || string.IsNullOrWhiteSpace(roomId))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        try
        {
            var accountId = await RoomGatewayWireMapper.ValidateAccountAsync(_clusterClient, req.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

            var leave = await _roomMembership.LeaveMappedRoomAsync(accountId, roomId);
            var result = leave.Operation;

            var wire = RoomGatewayWireMapper.ToRoomOperationRes(result, snapshot: null);
            var responsePayload = WireRoomGatewayBinary.Serialize(in wire);

            context.AccountId = accountId;
            if (result.Success)
                context.RoomId = string.Empty;
            else
                context.RoomId = leave.ActiveRoomId ?? roomId;

            return GatewayResponse.Ok(request.Seq, responsePayload.ToArray());
        }
        catch (Exception exception)
        {
            return RoomGatewayErrorMapper.ToResponse(request.Seq, exception);
        }
    }
}
