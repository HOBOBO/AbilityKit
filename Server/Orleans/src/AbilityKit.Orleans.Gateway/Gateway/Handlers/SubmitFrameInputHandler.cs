using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using Microsoft.Extensions.Options;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

[Core.GatewayHandler(OpCodes.SubmitFrameInput)]
public sealed partial class SubmitFrameInputHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly GatewayBattleInputGuard _inputGuard;
    private readonly Core.GatewayFrameSyncSubscriptionManager _subscriptions;
    private readonly BattleInputSecurityOptions _inputSecurityOptions;

    public SubmitFrameInputHandler(
        IClusterClient clusterClient,
        GatewayBattleInputGuard inputGuard,
        Core.GatewayFrameSyncSubscriptionManager subscriptions,
        IOptions<BattleInputSecurityOptions> inputSecurityOptions)
    {
        _clusterClient = clusterClient;
        _inputGuard = inputGuard;
        _subscriptions = subscriptions;
        _inputSecurityOptions = inputSecurityOptions.Value.Snapshot();
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

        WireSubmitFrameInputReq wireRequest;
        try
        {
            wireRequest = WireCustomBinary.DeserializeSubmitFrameInputReq(request.Payload);
        }
        catch (Exception)
        {
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
        }

        if (!IsValidRequest(wireRequest, _inputSecurityOptions))
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
            if (!CanSubmitInput(snapshot, wireRequest.WorldId, context.AccountId, wireRequest.PlayerId))
            {
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.Forbidden);
            }

            var battleKey = snapshot.BattleId ?? wireRequest.RoomId.ToString();
            var guard = _inputGuard.Check(
                context.SessionToken,
                battleKey,
                wireRequest.PlayerId,
                sequence: 0,
                nowTicks: DateTime.UtcNow.Ticks,
                lane: GatewayBattleInputLane.FrameInput);
            if (guard == GatewayBattleInputGuardResult.RateLimited)
            {
                return CreateInputResponse(
                    request.Seq,
                    accepted: false,
                    serverFrame: wireRequest.Frame,
                    FrameInputSubmitReason.RateLimited);
            }

            await _subscriptions.EnsureSubscribedAsync(context.ConnectionId, wireRequest.RoomId);

            var frameSync = _clusterClient.GetGrain<IBattleFrameSyncGrain>(wireRequest.RoomId.ToString());
            var result = await frameSync.SubmitInputWithResultAsync(
                wireRequest.WorldId,
                wireRequest.Frame,
                new FrameInputItem(
                    wireRequest.PlayerId,
                    wireRequest.InputOpCode,
                    wireRequest.InputPayload ?? Array.Empty<byte>()));

            return CreateInputResponse(request.Seq, result.Accepted, result.ServerFrame, result.Reason);
        }
        catch (Exception)
        {
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.InternalError);
        }
    }

    internal static bool IsValidRequest(
        WireSubmitFrameInputReq request,
        BattleInputSecurityOptions options)
    {
        return request.RoomId != 0
            && request.WorldId != 0
            && request.PlayerId != 0
            && request.Frame >= 0
            && request.InputOpCode > 0
            && request.InputOpCode <= options.MaxOpCode
            && (request.InputPayload?.Length ?? 0) <= options.MaxPayloadBytes;
    }

    internal static bool CanSubmitInput(
        RoomSnapshot? snapshot,
        ulong worldId,
        string? accountId,
        uint playerId)
    {
        if (snapshot == null
            || snapshot.Phase != RoomPhase.InBattle
            || worldId == 0
            || snapshot.WorldId != worldId
            || string.IsNullOrWhiteSpace(snapshot.BattleId)
            || string.IsNullOrWhiteSpace(accountId)
            || playerId == 0)
        {
            return false;
        }

        return RoomGatewayWireMapper.ResolvePlayerId(snapshot, accountId) == playerId;
    }

    private static GatewayResponse CreateInputResponse(
        uint requestSequence,
        bool accepted,
        int serverFrame,
        FrameInputSubmitReason reason)
    {
        var wire = new WireSubmitFrameInputRes(accepted, serverFrame, (int)reason);
        return GatewayResponse.Ok(requestSequence, WireCustomBinary.Serialize(in wire).ToArray());
    }
}
