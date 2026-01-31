using System.Collections.Concurrent;
using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using Microsoft.Extensions.Options;
using Orleans;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class FrameSyncObserverHub : IFrameSyncObserver
{
    private readonly IClusterClient _clusterClient;
    private readonly ITcpGatewaySessionRegistry _registry;
    private readonly IOptions<TcpGatewayOptions> _options;

    private readonly ConcurrentDictionary<string, IFrameSyncObserver> _observerRefs = new(StringComparer.Ordinal);

    public FrameSyncObserverHub(IClusterClient clusterClient, ITcpGatewaySessionRegistry registry, IOptions<TcpGatewayOptions> options)
    {
        _clusterClient = clusterClient;
        _registry = registry;
        _options = options;
    }

    public async Task EnsureSubscribedAsync(ulong roomId, CancellationToken cancellationToken)
    {
        var key = roomId.ToString();
        var obs = _observerRefs.GetOrAdd(key, _ => _clusterClient.CreateObjectReference<IFrameSyncObserver>(this));

        var grain = _clusterClient.GetGrain<IBattleFrameSyncGrain>(key);
        await grain.SubscribeAsync(obs);
    }

    public void OnFramePushed(FramePushedEvent evt)
    {
        // Fire-and-forget broadcast. This method is invoked by Orleans thread.
        _ = BroadcastAsync(evt);
    }

    private async Task BroadcastAsync(FramePushedEvent evt)
    {
        var mapper = _clusterClient.GetGrain<IRoomIdMappingGrain>("global");
        var roomId = await mapper.TryGetRoomIdAsync(evt.RoomId);
        if (string.IsNullOrWhiteSpace(roomId)) return;

        var room = _clusterClient.GetGrain<IRoomGrain>(roomId);
        var snapshot = await room.GetSnapshotAsync();

        if (snapshot?.Members == null || snapshot.Members.Count == 0) return;

        var wireInputs = evt.Inputs == null || evt.Inputs.Count == 0
            ? Array.Empty<WireInputItem>()
            : ToWireInputs(evt.Inputs);

        var push = new WireFramePushedPush(evt.RoomId, evt.WorldId, evt.Frame, wireInputs);
        var payload = WireCustomBinary.Serialize(in push);

        for (int i = 0; i < snapshot.Members.Count; i++)
        {
            var accountId = snapshot.Members[i];
            if (!_registry.TryGetConnectionIdByAccount(accountId, out var connectionId)) continue;
            if (!_registry.TryGetSession(connectionId, out var session) || session == null) continue;
            await session.SendServerPushAsync(opCode: _options.Value.FramePushedOpCode, payload: payload, CancellationToken.None);
        }
    }

    private static WireInputItem[] ToWireInputs(List<FrameInputItem> inputs)
    {
        var arr = new WireInputItem[inputs.Count];
        for (int i = 0; i < inputs.Count; i++)
        {
            var it = inputs[i];
            arr[i] = new WireInputItem(it.PlayerId, it.OpCode, it.Payload ?? Array.Empty<byte>());
        }
        return arr;
    }
}
