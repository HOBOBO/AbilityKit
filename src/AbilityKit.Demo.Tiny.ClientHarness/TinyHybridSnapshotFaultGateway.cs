using AbilityKit.Demo.Tiny.View;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Tiny.Client;

// The fault lives in the headless driver; production Gateway and Room behavior stay intact.
internal sealed class TinyHybridSnapshotFaultGateway : ITinyBattleGateway
{
    private readonly ITinyBattleGateway _inner;
    private WireStateSyncSnapshotPush? _fault;
    private bool _faultDelivered;
    private bool _holdFrames;
    private readonly Dictionary<int, uint> _authenticHashes = new();

    public TinyHybridSnapshotFaultGateway(ITinyBattleGateway inner) => _inner = inner;

    public IRoomGatewaySessionClient Rooms => _inner.Rooms;
    public RoomGatewayConnectionState ConnectionState => _inner.ConnectionState;
    public long ConnectionGeneration => _inner.ConnectionGeneration;
    public int FrameOverflowCount => _inner.FrameOverflowCount;
    public int DroppedFrames { get; private set; }
    public int AuthenticFrame { get; private set; } = -1;
    public uint AuthenticHash { get; private set; }

    public void Inject(WireStateSyncSnapshotPush snapshot)
    {
        if (_fault.HasValue || !snapshot.IsFullSnapshot || snapshot.Payload == null)
            throw new InvalidOperationException("Hybrid snapshot fault can be injected once.");
        _fault = snapshot;
        _holdFrames = true;
    }

    public bool TryGetAuthenticHash(int frame, out uint hash) =>
        _authenticHashes.TryGetValue(frame, out hash);

    public void ReleaseFrameStream() => _holdFrames = false;

    public async Task RequestAuthenticSnapshotAsync(
        string sessionToken, string roomId, string battleId, ulong worldId,
        int clientFrame, CancellationToken cancellationToken)
    {
        var response = await _inner.RequestFullSnapshotAsync(new WireRequestFullStateSyncReq
        {
            SessionToken = sessionToken,
            RoomId = roomId,
            BattleId = battleId,
            WorldId = worldId,
            ClientFrame = clientFrame,
            LastAuthoritativeFrame = clientFrame,
            Reason = "Hybrid mismatch test authority probe"
        }, cancellationToken);
        if (!response.Success || !response.Accepted)
            throw new InvalidOperationException(response.Message);
    }

    public bool TryGetLatest(out WireStateSyncSnapshotPush snapshot)
    {
        if (_fault.HasValue && !_faultDelivered)
        {
            _faultDelivered = true;
            snapshot = _fault.Value;
            return true;
        }
        if (!_inner.TryGetLatest(out snapshot)) return false;
        if (!_faultDelivered) return true;
        if (snapshot.Frame <= _fault!.Value.Frame) return false;
        if (snapshot.IsFullSnapshot && snapshot.PayloadOpCode == TinyBattleStateCodec.PayloadOpCode &&
            snapshot.Payload != null)
        {
            var authority = new TinyBattle();
            authority.RestoreState(TinyBattleStateCodec.Decode(snapshot.Payload));
            AuthenticFrame = snapshot.Frame;
            AuthenticHash = authority.ComputeHash();
            _authenticHashes[snapshot.Frame] = AuthenticHash;
        }
        return true;
    }

    public bool TryDequeueFrame(out WireRoomFramePush frame)
    {
        if (_holdFrames)
        {
            while (_inner.TryDequeueFrame(out _)) DroppedFrames++;
            frame = default;
            return false;
        }
        return _inner.TryDequeueFrame(out frame);
    }

    public bool TryGetFrameOverflow(out int minimumRecoveryFrame) =>
        _inner.TryGetFrameOverflow(out minimumRecoveryFrame);
    public void Tick(float deltaTime) => _inner.Tick(deltaTime);
    public bool CompleteRestore(long generation) => _inner.CompleteRestore(generation);
    public void PrepareFullSnapshotRecovery() => _inner.PrepareFullSnapshotRecovery();
    public void ResumeFrameStream() => _inner.ResumeFrameStream();
    public Task<WireRequestFullStateSyncRes> RequestFullSnapshotAsync(
        WireRequestFullStateSyncReq request, CancellationToken cancellationToken) =>
        _inner.RequestFullSnapshotAsync(request, cancellationToken);
    public Task<WireRoomSubscribeFrameSyncRes> SubscribeFrameSyncAsync(
        WireRoomSubscribeFrameSyncReq request, CancellationToken cancellationToken) =>
        _inner.SubscribeFrameSyncAsync(request, cancellationToken);
    public Task<WireRoomSubmitFrameInputRes> SubmitFrameInputAsync(
        WireRoomSubmitFrameInputReq request, CancellationToken cancellationToken) =>
        _inner.SubmitFrameInputAsync(request, cancellationToken);
    public Task<WireSubmitBattleInputRes> SubmitAsync(
        string sessionToken, string battleId, ulong worldId, uint playerId,
        TinyInput input, ulong sequence, CancellationToken cancellationToken) =>
        _inner.SubmitAsync(sessionToken, battleId, worldId, playerId, input, sequence, cancellationToken);
    public void Dispose() => _inner.Dispose();
}
