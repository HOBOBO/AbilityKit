using AbilityKit.Demo.Tiny;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Tiny.Client;

public sealed class TinyNetworkClient : IDisposable
{
    private readonly RoomGatewayConnectionSession _connection;
    private readonly object _snapshotGate = new();
    private readonly SortedDictionary<int, WireStateSyncSnapshotPush> _snapshots = new();
    private readonly Queue<WireRoomFramePush> _frames = new();

    private TinyNetworkClient(RoomGatewayConnectionSession connection)
    {
        _connection = connection;
        _connection.RoomClient.StateSyncSnapshotReceived += OnSnapshot;
        _connection.RoomClient.FrameSyncFrameReceived += OnFrame;
    }

    public string AccountId { get; private set; } = string.Empty;
    public string SessionToken { get; private set; } = string.Empty;
    public IRoomGatewaySessionClient Rooms => _connection.RoomClient;
    public RoomGatewayWireSessionClient WireRooms => _connection.RoomClient;
    public void Tick(float deltaTime) => _connection.Tick(deltaTime);

    public bool TryDequeueFrame(out WireRoomFramePush frame)
    {
        lock (_snapshotGate)
        {
            if (_frames.Count > 0)
            {
                frame = _frames.Dequeue();
                return true;
            }
        }
        frame = default;
        return false;
    }

    public int LatestNetworkFrame { get; private set; } = -1;

    public static async Task<TinyNetworkClient> ConnectAsync(string host, int port, string accountId, CancellationToken cancellationToken)
    {
        var connection = await RoomGatewayConnectionSession.ConnectAsync(
            host, port, timeout: TimeSpan.FromSeconds(10), cancellationToken: cancellationToken);
        TinyNetworkClient? client = null;
        try
        {
            client = new TinyNetworkClient(connection);
            var result = await connection.RoomClient.AccountLoginAsync(
                accountId, kickExisting: true, TimeSpan.FromSeconds(10), cancellationToken);
            if (!result.Success) throw new InvalidOperationException($"Login failed: {result.Message}");
            client.AccountId = result.AccountId;
            client.SessionToken = result.SessionToken;
            return client;
        }
        catch
        {
            if (client != null) client.Dispose();
            else connection.Dispose();
            throw;
        }
    }

    public async Task<WireSubmitBattleInputRes> SubmitAsync(
        string battleId, ulong worldId, uint playerId, TinyInput input, ulong sequence,
        CancellationToken cancellationToken)
    {
        var request = new WireSubmitBattleInputReq
        {
            SessionToken = SessionToken,
            BattleId = battleId,
            WorldId = worldId,
            Frame = 0,
            PlayerId = playerId,
            InputOpCode = TinyBattle.InputOpCode,
            Payload = input.Encode(),
            CommandSequence = sequence
        };
        return await _connection.RoomClient.SubmitBattleInputAsync(
            request, TimeSpan.FromSeconds(10), cancellationToken);
    }

    public bool TryGetSnapshot(int frame, out WireStateSyncSnapshotPush snapshot)
    {
        lock (_snapshotGate) return _snapshots.TryGetValue(frame, out snapshot);
    }

    public bool TryGetFullSnapshot(ulong worldId, out WireStateSyncSnapshotPush snapshot)
    {
        lock (_snapshotGate)
        {
            foreach (var candidate in _snapshots.Values)
            {
                if (!candidate.IsFullSnapshot || candidate.WorldId != worldId) continue;
                snapshot = candidate;
                return true;
            }
        }
        snapshot = default;
        return false;
    }

    public int LatestFrame
    {
        get
        {
            lock (_snapshotGate) return _snapshots.Count == 0 ? -1 : _snapshots.Last().Key;
        }
    }

    private void OnSnapshot(WireStateSyncSnapshotPush snapshot)
    {
        lock (_snapshotGate)
        {
            _snapshots[snapshot.Frame] = snapshot;
            while (_snapshots.Count > 128) _snapshots.Remove(_snapshots.First().Key);
        }
    }

    private void OnFrame(WireRoomFramePush frame)
    {
        lock (_snapshotGate)
        {
            LatestNetworkFrame = Math.Max(LatestNetworkFrame, frame.Frame);
            if (_frames.Count >= 256) _frames.Dequeue();
            _frames.Enqueue(frame);
        }
    }

    public void Dispose()
    {
        _connection.RoomClient.StateSyncSnapshotReceived -= OnSnapshot;
        _connection.RoomClient.FrameSyncFrameReceived -= OnFrame;
        _connection.Dispose();
    }
}
