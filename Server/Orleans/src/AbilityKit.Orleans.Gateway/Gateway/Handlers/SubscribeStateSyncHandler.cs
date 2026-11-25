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
    private readonly Core.GatewayStateSyncPushSubscriptionManager _pushSubscriptions;
    private readonly ILogger<SubscribeStateSyncHandler> _logger;

    public SubscribeStateSyncHandler(
        IClusterClient clusterClient,
        IGatewaySessionRegistry sessionRegistry,
        Core.GatewayStateSyncPushSubscriptionManager pushSubscriptions,
        ILogger<SubscribeStateSyncHandler> logger)
    {
        _clusterClient = clusterClient;
        _sessionRegistry = sessionRegistry;
        _pushSubscriptions = pushSubscriptions;
        _logger = logger;
    }

    public override async ValueTask<GatewayResponse> HandleAsync(
        GatewayRequest request,
        GatewaySessionContext context,
        CancellationToken cancellationToken)
        => await SubscribeAsync(request, context, fixedBaseline: false);

    internal ValueTask<GatewayResponse> SubscribeAsync(
        GatewayRequest request, GatewaySessionContext context, bool fixedBaseline)
    {
        if (request.Payload == null || request.Payload.Length == 0)
            return ValueTask.FromResult(GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest));

        var req = WireRoomGatewayBinary.Deserialize<WireSubscribeStateSyncReq>(request.Payload);
        return SubscribeAsync(request.Seq, context, req, fixedBaseline);
    }

    internal async ValueTask<GatewayResponse> SubscribeAsync(uint requestSequence, GatewaySessionContext context,
        WireSubscribeStateSyncReq req, bool fixedBaseline, Guid inputGeneration = default, ulong worldId = 0,
        uint playerId = 0, bool receivesMovementProof = false)
    {
        if (string.IsNullOrWhiteSpace(req.SessionToken)
            || string.IsNullOrWhiteSpace(req.BattleId)
            || string.IsNullOrWhiteSpace(req.RoomId)
            || context.ConnectionId <= 0)
        {
            return GatewayResponse.Error(requestSequence, GatewayStatusCode.BadRequest);
        }

        try
        {
            var accountId = await RoomGatewayWireMapper.ValidateAccountAsync(_clusterClient, req.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return GatewayResponse.Error(requestSequence, GatewayStatusCode.BadRequest);
            }

            if (inputGeneration != Guid.Empty)
            {
                var membership = _clusterClient.GetGrain<IRoomIdMappingGrain>("global");
                var roomId = await membership.TryGetAccountRoomAsync(accountId);
                if (string.IsNullOrWhiteSpace(roomId))
                    return GatewayResponse.Error(requestSequence, GatewayStatusCode.BadRequest);
                var roomSnapshot = await _clusterClient.GetGrain<IRoomGrain>(roomId).GetSnapshotAsync();
                if (!string.Equals(roomSnapshot?.Summary.RoomId, req.RoomId, StringComparison.Ordinal) ||
                    !SubmitBattleInputHandler.CanUseScopedInput(roomSnapshot, req.BattleId, worldId, accountId, playerId))
                    return GatewayResponse.Error(requestSequence, GatewayStatusCode.BadRequest);
            }

            context.AccountId = accountId;
            if (context.ConnectionId > 0)
            {
                _sessionRegistry.BindAccount(accountId, context.ConnectionId);
            }

            var roomKey = string.IsNullOrWhiteSpace(req.RoomId) ? req.BattleId : req.RoomId;
            var observerKey = $"{accountId}:{roomKey}";
            var observerGrain = _clusterClient.GetGrain<IStateSyncObserverGrain>(observerKey);

            context.ScopedStateSyncInput = null;
            var recipient = await _pushSubscriptions.EnsureBoundAsync(context, observerKey);
            var cursor = new ReliableBattleEventSubscribeCursor
            {
                Epoch = req.EventEpoch ?? string.Empty,
                LastAcknowledgedSequence = Math.Max(0, req.LastEventAck)
            };
            if (fixedBaseline)
                await observerGrain.SubscribeFixedBaselineAsync(req.BattleId, cursor,
                    recipient with
                    {
                        SubscriptionGeneration = Guid.NewGuid(), InputGeneration = inputGeneration, PlayerId = playerId,
                        ReceivesMovementProof = receivesMovementProof
                    });
            else
                await observerGrain.SubscribeAsync(req.BattleId, cursor);

            context.ScopedStateSyncInput = inputGeneration == Guid.Empty ? null :
                new ScopedStateSyncInputIdentity(accountId, req.BattleId, worldId, playerId, inputGeneration);

            var wire = new WireSubscribeStateSyncRes
            {
                Success = true,
                Message = string.Empty
            };
            var responsePayload = WireRoomGatewayBinary.Serialize(in wire);
            return GatewayResponse.Ok(requestSequence, responsePayload.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Subscribe state sync failed. BattleId={BattleId}, RoomId={RoomId}, ConnectionId={ConnectionId}", req.BattleId, req.RoomId, context.ConnectionId);
            return GatewayResponse.Error(requestSequence, GatewayStatusCode.InternalError);
        }
    }
}


