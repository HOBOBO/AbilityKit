using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Orleans.Gateway.Core;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

[GatewayHandler(RoomGatewayOpCodes.SubscribeFrameSync)]
public sealed partial class RoomSubscribeFrameSyncHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly GatewayFrameSyncSubscriptionManager _subscriptions;

    public RoomSubscribeFrameSyncHandler(IClusterClient clusterClient,
        GatewayFrameSyncSubscriptionManager subscriptions)
    {
        _clusterClient = clusterClient;
        _subscriptions = subscriptions;
    }

    public override async ValueTask<GatewayResponse> HandleAsync(GatewayRequest request,
        GatewaySessionContext context, CancellationToken cancellationToken)
    {
        if (request.Payload is not { Length: > 0 })
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
        WireRoomSubscribeFrameSyncReq wire;
        try { wire = WireRoomGatewayBinary.Deserialize<WireRoomSubscribeFrameSyncReq>(request.Payload); }
        catch { return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest); }
        if (string.IsNullOrWhiteSpace(wire.SessionToken) || string.IsNullOrWhiteSpace(wire.RoomId) ||
            string.IsNullOrWhiteSpace(wire.BattleId) || wire.WorldId == 0 || context.ConnectionId <= 0)
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        var account = await RoomGatewayWireMapper.ValidateAccountAsync(_clusterClient, wire.SessionToken);
        if (string.IsNullOrEmpty(account)) return GatewayResponse.Error(request.Seq, GatewayStatusCode.Unauthorized);
        var mapping = _clusterClient.GetGrain<IRoomIdMappingGrain>("global");
        if (await mapping.TryGetAccountRoomAsync(account) != wire.RoomId)
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.Forbidden);
        var room = _clusterClient.GetGrain<IRoomGrain>(wire.RoomId);
        var snapshot = await room.GetSnapshotAsync();
        if (snapshot.Phase != RoomPhase.InBattle || snapshot.BattleId != wire.BattleId ||
            snapshot.WorldId != wire.WorldId || !snapshot.Members.Contains(account) ||
            !RoomFrameSyncAuthorization.SupportsFrameSync(snapshot))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.Forbidden);

        var numericRoomId = await mapping.GetOrCreateNumericIdAsync(wire.RoomId);
        await _subscriptions.EnsureSubscribedAsync(context.ConnectionId, numericRoomId,
            GatewayFrameSyncSubscriptionManager.PushFormat.Room);
        var result = new WireRoomSubscribeFrameSyncRes { Success = true, Message = string.Empty };
        return GatewayResponse.Ok(request.Seq, WireRoomGatewayBinary.Serialize(in result).ToArray());
    }
}

[GatewayHandler(RoomGatewayOpCodes.SubmitFrameInput)]
public sealed partial class RoomSubmitFrameInputHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly GatewayBattleInputGuard _inputGuard;

    public RoomSubmitFrameInputHandler(IClusterClient clusterClient, GatewayBattleInputGuard inputGuard)
    {
        _clusterClient = clusterClient;
        _inputGuard = inputGuard;
    }

    public override async ValueTask<GatewayResponse> HandleAsync(GatewayRequest request,
        GatewaySessionContext context, CancellationToken cancellationToken)
    {
        if (request.Payload is not { Length: > 0 })
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
        WireRoomSubmitFrameInputReq wire;
        try { wire = WireRoomGatewayBinary.Deserialize<WireRoomSubmitFrameInputReq>(request.Payload); }
        catch { return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest); }
        if (string.IsNullOrWhiteSpace(wire.SessionToken) || string.IsNullOrWhiteSpace(wire.RoomId) ||
            string.IsNullOrWhiteSpace(wire.BattleId) || wire.WorldId == 0 || wire.PlayerId == 0 ||
            wire.Frame < 0 || wire.InputOpCode <= 0 || (wire.Payload?.Length ?? 0) > 4096)
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        var account = await RoomGatewayWireMapper.ValidateAccountAsync(_clusterClient, wire.SessionToken);
        if (string.IsNullOrEmpty(account)) return GatewayResponse.Error(request.Seq, GatewayStatusCode.Unauthorized);
        var mapping = _clusterClient.GetGrain<IRoomIdMappingGrain>("global");
        if (await mapping.TryGetAccountRoomAsync(account) != wire.RoomId)
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.Forbidden);
        var room = _clusterClient.GetGrain<IRoomGrain>(wire.RoomId);
        var snapshot = await room.GetSnapshotAsync();
        if (snapshot.BattleId != wire.BattleId ||
            !SubmitFrameInputHandler.CanSubmitInput(snapshot, wire.WorldId, account, wire.PlayerId) ||
            !RoomFrameSyncAuthorization.SupportsFrameSync(snapshot))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.Forbidden);

        var guard = _inputGuard.Check(wire.SessionToken, wire.BattleId, wire.PlayerId,
            sequence: 0, nowTicks: DateTime.UtcNow.Ticks, lane: GatewayBattleInputLane.FrameInput);
        if (guard == GatewayBattleInputGuardResult.RateLimited)
        {
            var limited = new WireRoomSubmitFrameInputRes
            {
                Accepted = false,
                ServerFrame = wire.Frame,
                Reason = (int)FrameInputSubmitReason.RateLimited
            };
            return GatewayResponse.Ok(request.Seq, WireRoomGatewayBinary.Serialize(in limited).ToArray());
        }

        var numericRoomId = await mapping.GetOrCreateNumericIdAsync(wire.RoomId);
        var grain = _clusterClient.GetGrain<IBattleFrameSyncGrain>(numericRoomId.ToString());
        var submitted = await grain.SubmitInputWithResultAsync(wire.WorldId, wire.Frame,
            new FrameInputItem(wire.PlayerId, wire.InputOpCode, wire.Payload ?? Array.Empty<byte>()));
        var result = new WireRoomSubmitFrameInputRes
        {
            Accepted = submitted.Accepted,
            ServerFrame = submitted.ServerFrame,
            Reason = (int)submitted.Reason
        };
        return GatewayResponse.Ok(request.Seq, WireRoomGatewayBinary.Serialize(in result).ToArray());
    }
}

internal static class RoomFrameSyncAuthorization
{
    public static bool SupportsFrameSync(RoomSnapshot snapshot) =>
        snapshot.SyncCapabilities != null &&
        (((InputPolicy)snapshot.SyncCapabilities.Input & InputPolicy.DeterministicBroadcast) != 0);
}
