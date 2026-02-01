using System.Collections.Concurrent;
using System.Diagnostics;
using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class FrameSyncObserverHub : IFrameSyncObserver
{
    private static readonly bool EnablePushStatsLog = false;

    private readonly IClusterClient _clusterClient;
    private readonly ITcpGatewaySessionRegistry _registry;
    private readonly IOptions<TcpGatewayOptions> _options;
    private readonly ILogger<FrameSyncObserverHub> _logger;

    private readonly ConcurrentDictionary<string, IFrameSyncObserver> _observerRefs = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<ulong, CachedRoomId> _roomIdCache = new();
    private readonly ConcurrentDictionary<string, CachedRoomMembers> _roomMembersCache = new(StringComparer.Ordinal);

    private readonly TimeSpan _roomIdCacheTtl = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _roomMembersCacheTtl = TimeSpan.FromMilliseconds(250);

    private sealed record CachedRoomId(string RoomId, DateTime UpdatedAtUtc);
    private sealed record CachedRoomMembers(List<string> Members, DateTime UpdatedAtUtc);

    private readonly object _statsLock = new();
    private DateTime _statsWindowStartUtc = DateTime.UtcNow;
    private int _statsFrames;
    private double _msTotal;
    private double _msMap;
    private double _msMembers;
    private double _msSerialize;
    private double _msSend;

    public FrameSyncObserverHub(IClusterClient clusterClient, ITcpGatewaySessionRegistry registry, IOptions<TcpGatewayOptions> options, ILogger<FrameSyncObserverHub> logger)
    {
        _clusterClient = clusterClient;
        _registry = registry;
        _options = options;
        _logger = logger;
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
        var swTotal = Stopwatch.StartNew();
        var msMap = 0d;
        var msMembers = 0d;
        var msSerialize = 0d;
        var msSend = 0d;

        var now = DateTime.UtcNow;

        string? roomId = null;
        if (_roomIdCache.TryGetValue(evt.RoomId, out var cachedRoomId) && (now - cachedRoomId.UpdatedAtUtc) <= _roomIdCacheTtl)
        {
            roomId = cachedRoomId.RoomId;
        }
        else
        {
            var sw = Stopwatch.StartNew();
            var mapper = _clusterClient.GetGrain<IRoomIdMappingGrain>("global");
            roomId = await mapper.TryGetRoomIdAsync(evt.RoomId);
            sw.Stop();
            msMap += sw.Elapsed.TotalMilliseconds;
            if (!string.IsNullOrWhiteSpace(roomId))
            {
                _roomIdCache[evt.RoomId] = new CachedRoomId(roomId, now);
            }
        }

        if (string.IsNullOrWhiteSpace(roomId))
        {
            _logger.LogWarning("FramePushed dropped: numeric roomId has no mapping. NumericRoomId={NumericRoomId} WorldId={WorldId} Frame={Frame}", evt.RoomId, evt.WorldId, evt.Frame);
            return;
        }

        List<string>? members = null;
        if (_roomMembersCache.TryGetValue(roomId, out var cachedMembers) && (now - cachedMembers.UpdatedAtUtc) <= _roomMembersCacheTtl)
        {
            members = cachedMembers.Members;
        }
        else
        {
            var sw = Stopwatch.StartNew();
            var room = _clusterClient.GetGrain<IRoomGrain>(roomId);
            var snapshot = await room.GetSnapshotAsync();
            members = snapshot?.Members;
            sw.Stop();
            msMembers += sw.Elapsed.TotalMilliseconds;
            if (members != null)
            {
                _roomMembersCache[roomId] = new CachedRoomMembers(members, now);
            }
        }

        if (members == null || members.Count == 0)
        {
            _logger.LogWarning("FramePushed dropped: room has no members. RoomId={RoomId} NumericRoomId={NumericRoomId} WorldId={WorldId} Frame={Frame}", roomId, evt.RoomId, evt.WorldId, evt.Frame);
            return;
        }

        ArraySegment<byte> payload;
        {
            var sw = Stopwatch.StartNew();
            var wireInputs = evt.Inputs == null || evt.Inputs.Count == 0
                ? Array.Empty<WireInputItem>()
                : ToWireInputs(evt.Inputs);

            var push = new WireFramePushedPush(evt.RoomId, evt.WorldId, evt.Frame, wireInputs);
            payload = WireCustomBinary.Serialize(in push);
            sw.Stop();
            msSerialize += sw.Elapsed.TotalMilliseconds;
        }

        var tasks = new List<Task>(members.Count);

        for (int i = 0; i < members.Count; i++)
        {
            var accountId = members[i];
            if (!_registry.TryGetConnectionIdByAccount(accountId, out var connectionId))
            {
                continue;
            }
            if (!_registry.TryGetSession(connectionId, out var session) || session == null)
            {
                continue;
            }

            tasks.Add(session.SendServerPushAsync(opCode: _options.Value.FramePushedOpCode, payload: payload, CancellationToken.None));
        }

        if (tasks.Count > 0)
        {
            var sw = Stopwatch.StartNew();
            await Task.WhenAll(tasks);
            sw.Stop();
            msSend += sw.Elapsed.TotalMilliseconds;
        }

        swTotal.Stop();

        lock (_statsLock)
        {
            _statsFrames++;
            _msTotal += swTotal.Elapsed.TotalMilliseconds;
            _msMap += msMap;
            _msMembers += msMembers;
            _msSerialize += msSerialize;
            _msSend += msSend;

            var elapsed = (now - _statsWindowStartUtc).TotalSeconds;
            if (elapsed >= 1.0)
            {
                var hz = elapsed > 0 ? _statsFrames / elapsed : 0;
                var avgTotal = _statsFrames > 0 ? _msTotal / _statsFrames : 0;
                var avgMap = _statsFrames > 0 ? _msMap / _statsFrames : 0;
                var avgMembers = _statsFrames > 0 ? _msMembers / _statsFrames : 0;
                var avgSerialize = _statsFrames > 0 ? _msSerialize / _statsFrames : 0;
                var avgSend = _statsFrames > 0 ? _msSend / _statsFrames : 0;

                if (EnablePushStatsLog)
                {
                    _logger.LogInformation("[FrameSyncObserverHub] Push stats. RoomId={RoomId} Hz={Hz:F1} AvgMsTotal={AvgTotal:F2} AvgMsMap={AvgMap:F2} AvgMsMembers={AvgMembers:F2} AvgMsSerialize={AvgSerialize:F2} AvgMsSend={AvgSend:F2}",
                        roomId, hz, avgTotal, avgMap, avgMembers, avgSerialize, avgSend);
                }

                _statsWindowStartUtc = now;
                _statsFrames = 0;
                _msTotal = 0;
                _msMap = 0;
                _msMembers = 0;
                _msSerialize = 0;
                _msSend = 0;
            }
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
