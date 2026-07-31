using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Room;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

/// <summary>
/// 鎴块棿鑻遍泟閫夋嫨 Handler
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.PickHero)]
public sealed partial class RoomPickHeroHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;

    public RoomPickHeroHandler(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public override async ValueTask<GatewayResponse> HandleAsync(
        GatewayRequest request,
        GatewaySessionContext context,
        CancellationToken cancellationToken)
    {
        if (request.Payload == null || request.Payload.Length == 0)
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        var req = WireRoomGatewayBinary.Deserialize<WireRoomPickHeroReq>(request.Payload);
        var roomId = string.IsNullOrWhiteSpace(req.RoomId) ? context.RoomId : req.RoomId;
        if (string.IsNullOrWhiteSpace(req.SessionToken) || string.IsNullOrWhiteSpace(roomId))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        try
        {
            var accountId = await RoomGatewayWireMapper.ValidateAccountAsync(_clusterClient, req.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

            var room = _clusterClient.GetGrain<IRoomGrain>(roomId);
            var result = await room.SubmitGameplayCommandWithResultAsync(
                RoomGatewayWireMapper.ToGameplayCommand(accountId, req));

            var snapshot = await room.GetSnapshotAsync();
            var wire = RoomGatewayWireMapper.ToSnapshotRes(snapshot, result);
            var responsePayload = WireRoomGatewayBinary.Serialize(in wire);

            context.RoomId = roomId;
            context.AccountId = accountId;
            return GatewayResponse.Ok(request.Seq, responsePayload.ToArray());
        }
        catch (Exception exception)
        {
            return RoomGatewayErrorMapper.ToResponse(request.Seq, exception);
        }
    }
}

