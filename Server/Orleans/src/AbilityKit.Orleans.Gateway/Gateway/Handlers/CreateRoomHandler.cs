using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Room;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

/// <summary>
/// 鍒涘缓鎴块棿 Handler
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.CreateRoom)]
public sealed partial class CreateRoomHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;

    public CreateRoomHandler(IClusterClient clusterClient)
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

        var req = WireRoomGatewayBinary.Deserialize<WireCreateRoomReq>(request.Payload);
        if (string.IsNullOrWhiteSpace(req.SessionToken) || string.IsNullOrWhiteSpace(req.Region) || string.IsNullOrWhiteSpace(req.ServerId))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        try
        {
            var accountId = await RoomGatewayWireMapper.ValidateAccountAsync(_clusterClient, req.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

            var directoryKey = $"{req.Region}:{req.ServerId}";
            var directory = _clusterClient.GetGrain<IRoomDirectoryGrain>(directoryKey);

            var resp = await directory.CreateRoomAsync(new CreateRoomRequest(
                accountId,
                req.Region,
                req.ServerId,
                string.IsNullOrWhiteSpace(req.RoomType) ? GameplayRoomTypes.Default : req.RoomType,
                req.Title ?? string.Empty,
                req.IsPublic,
                req.MaxPlayers,
                req.Tags,
                req.CommandId));

            if (!string.IsNullOrEmpty(resp.RoomId))
            {
                var mapping = _clusterClient.GetGrain<IRoomIdMappingGrain>("global");
                if (!await mapping.TryBindAccountRoomIfActiveAsync(accountId, resp.RoomId))
                    throw new InvalidOperationException("The room created by this command is no longer active for its creator.");

                context.RoomId = resp.RoomId;
                context.AccountId = accountId;
            }

            var wire = new WireCreateRoomRes
            {
                Success = true,
                RoomId = resp.RoomId ?? string.Empty,
                NumericRoomId = RoomGatewayIds.CreateNumericRoomId(resp.RoomId),
                Message = string.Empty
            };
            var responsePayload = WireRoomGatewayBinary.Serialize(in wire);
            return GatewayResponse.Ok(request.Seq, responsePayload.ToArray());
        }
        catch (Exception exception)
        {
            return RoomGatewayErrorMapper.ToResponse(request.Seq, exception);
        }
    }
}

