using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Room;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

/// <summary>
/// 璁㈤槄鐘舵€佸悓姝?Handler
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.SubscribeStateSync)]
public sealed partial class SubscribeStateSyncHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly IGatewaySessionRegistry _sessionRegistry;
    private readonly ILogger<SubscribeStateSyncHandler> _logger;

    public SubscribeStateSyncHandler(
        IClusterClient clusterClient,
        IGatewaySessionRegistry sessionRegistry,
        ILogger<SubscribeStateSyncHandler> logger)
    {
        _clusterClient = clusterClient;
        _sessionRegistry = sessionRegistry;
        _logger = logger;
    }

    public override async ValueTask<GatewayResponse> HandleAsync(
        GatewayRequest request,
        GatewaySessionContext context,
        CancellationToken cancellationToken)
    {
        if (request.Payload == null || request.Payload.Length == 0)
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        var req = WireRoomGatewayBinary.Deserialize<WireSubscribeStateSyncReq>(request.Payload);
        if (string.IsNullOrWhiteSpace(req.SessionToken)
            || string.IsNullOrWhiteSpace(req.BattleId)
            || string.IsNullOrWhiteSpace(req.RoomId)
            || context.ConnectionId <= 0)
        {
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
        }

        try
        {
            var accountId = await RoomGatewayWireMapper.ValidateAccountAsync(_clusterClient, req.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
            }

            context.AccountId = accountId;
            if (context.ConnectionId > 0)
            {
                _sessionRegistry.BindAccount(accountId, context.ConnectionId);
            }

            var roomKey = string.IsNullOrWhiteSpace(req.RoomId) ? req.BattleId : req.RoomId;
            var observerKey = $"{accountId}:{roomKey}";
            var observerGrain = _clusterClient.GetGrain<IStateSyncObserverGrain>(observerKey);

            await observerGrain.SubscribeAsync(req.BattleId, new ReliableBattleEventSubscribeCursor
            {
                Epoch = req.EventEpoch ?? string.Empty,
                LastAcknowledgedSequence = Math.Max(0, req.LastEventAck)
            });

            var wire = new WireSubscribeStateSyncRes
            {
                Success = true,
                Message = string.Empty
            };
            var responsePayload = WireRoomGatewayBinary.Serialize(in wire);
            return GatewayResponse.Ok(request.Seq, responsePayload.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Subscribe state sync failed. BattleId={BattleId}, RoomId={RoomId}, ConnectionId={ConnectionId}", req.BattleId, req.RoomId, context.ConnectionId);
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.InternalError);
        }
    }
}


