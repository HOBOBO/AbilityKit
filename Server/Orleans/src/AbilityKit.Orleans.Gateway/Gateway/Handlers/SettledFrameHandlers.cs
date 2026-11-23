using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Orleans.Gateway.Core;
using AbilityKit.Protocol.Moba.SettledFrames;
using MemoryPack;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

[GatewayHandler(SettledFrameProtocol.Subscribe)]
public sealed partial class SubscribeSettledFrameHandler(IClusterClient clusterClient,
    GatewayFrameSyncSubscriptionManager subscriptions) : GatewayRequestHandlerBase
{
    public override async ValueTask<GatewayResponse> HandleAsync(GatewayRequest request, GatewaySessionContext context,
        CancellationToken cancellationToken)
    {
        if (context.ConnectionId <= 0 || string.IsNullOrWhiteSpace(context.AccountId) || string.IsNullOrWhiteSpace(context.SessionToken))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.Unauthorized);
        if (request.Payload is not { Length: > 0 and <= 4096 })
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
        WireSettledSubscribeRequest wire;
        try { wire = MemoryPackSerializer.Deserialize<WireSettledSubscribeRequest>(request.Payload); }
        catch { return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest); }
        if (wire.Version != SettledFrameProtocol.Version)
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.SettledFrameVersionMismatch);
        if (wire.RoomId == 0 || wire.WorldId == 0 || string.IsNullOrWhiteSpace(wire.BattleId))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        var mapping = clusterClient.GetGrain<IRoomIdMappingGrain>("global");
        var roomId = await mapping.TryGetRoomIdAsync(wire.RoomId);
        if (string.IsNullOrWhiteSpace(roomId)) return GatewayResponse.Error(request.Seq, GatewayStatusCode.NotFound);
        var room = await clusterClient.GetGrain<IRoomGrain>(roomId).GetSnapshotAsync();
        if (room.Phase != RoomPhase.InBattle || room.BattleId != wire.BattleId || room.WorldId != wire.WorldId ||
            !RoomFrameSyncAuthorization.SupportsFrameSync(room) ||
            (!wire.Spectator && (!room.Members.Contains(context.AccountId) || await mapping.TryGetAccountRoomAsync(context.AccountId) != roomId)))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.Forbidden);

        var grain = clusterClient.GetGrain<IBattleFrameSyncGrain>(wire.RoomId.ToString());
        var binding = await grain.GetSettledBindingAsync();
        if (binding == null) return GatewayResponse.Error(request.Seq, GatewayStatusCode.SettledFrameVersionMismatch);
        if (binding.Faulted || binding.RoomId != wire.RoomId || binding.WorldId != wire.WorldId || binding.BattleId != wire.BattleId ||
            !await subscriptions.SubscribeSettledAsync(context.ConnectionId, context.AccountId, binding, wire.Spectator, grain))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.Conflict);

        return GatewayResponse.Ok(request.Seq, MemoryPackSerializer.Serialize(new WireSettledSubscribeResponse
        {
            Identity = GatewaySettledFrameWire.Identity(binding.RoomId, binding.WorldId, binding.BattleId, binding.RuntimeGeneration),
            TickRate = binding.TickRate, LastSettledFrame = binding.LastSettledFrame
        }));
    }
}

[GatewayHandler(SettledFrameProtocol.CatchUp)]
public sealed partial class CatchUpSettledFrameHandler(IClusterClient clusterClient,
    GatewayFrameSyncSubscriptionManager subscriptions) : GatewayRequestHandlerBase
{
    public override async ValueTask<GatewayResponse> HandleAsync(GatewayRequest request, GatewaySessionContext context,
        CancellationToken cancellationToken)
    {
        if (context.ConnectionId <= 0 || string.IsNullOrWhiteSpace(context.AccountId) || string.IsNullOrWhiteSpace(context.SessionToken))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.Unauthorized);
        if (request.Payload is not { Length: > 0 and <= 4096 })
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
        WireSettledCatchUpRequest wire;
        try { wire = MemoryPackSerializer.Deserialize<WireSettledCatchUpRequest>(request.Payload); }
        catch { return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest); }
        var identity = wire.Identity;
        if (identity.Version != SettledFrameProtocol.Version)
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.SettledFrameVersionMismatch);
        if (identity.RoomId == 0 || identity.WorldId == 0 || string.IsNullOrWhiteSpace(identity.BattleId) ||
            !Guid.TryParseExact(identity.RuntimeGeneration, "N", out var generation) || generation == Guid.Empty ||
            wire.FromFrameExclusive < -1 || wire.ToFrameInclusive <= wire.FromFrameExclusive)
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        var grain = clusterClient.GetGrain<IBattleFrameSyncGrain>(identity.RoomId.ToString());
        var binding = await grain.GetSettledBindingAsync();
        if (binding == null) return GatewayResponse.Error(request.Seq, GatewayStatusCode.SettledFrameVersionMismatch);
        if (binding.RoomId != identity.RoomId || binding.WorldId != identity.WorldId || binding.BattleId != identity.BattleId ||
            binding.RuntimeGeneration != generation || !subscriptions.HasSettledBinding(context, binding, requiresPlayer: false))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.Forbidden);

        var result = await grain.RequestSettledCatchUpAsync(new FrameSyncSettledCatchUpRequest(identity.RoomId,
            identity.WorldId, generation, wire.FromFrameExclusive, wire.ToFrameInclusive));
        if (result == null) return GatewayResponse.Error(request.Seq, GatewayStatusCode.NotFound);
        var payload = MemoryPackSerializer.Serialize(new WireSettledCatchUpResponse
        {
            Frames = result.Frames.Select(GatewaySettledFrameWire.ToWire).ToArray()
        });
        return payload.Length <= SettledFrameProtocol.MaxPayloadBytes
            ? GatewayResponse.Ok(request.Seq, payload)
            : GatewayResponse.Error(request.Seq, GatewayStatusCode.Conflict);
    }
}
