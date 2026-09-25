#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Tiny.View
{
    internal sealed class TinyGatewayClient : ITinyBattleGateway
    {
        private readonly RoomGatewayConnectionSession _connection;
        private readonly object _snapshotGate = new object();
        private WireStateSyncSnapshotPush? _latest;
        private readonly RoomGatewayFrameInbox _frames = new RoomGatewayFrameInbox();

        private TinyGatewayClient(RoomGatewayConnectionSession connection)
        {
            _connection = connection;
            _connection.RoomClient.StateSyncSnapshotReceived += OnSnapshot;
            _connection.RoomClient.FrameSyncFrameReceived += OnFrame;
        }

        public IRoomGatewaySessionClient Rooms => _connection.RoomClient;
        public RoomGatewayConnectionState ConnectionState => _connection.Recovery.State;
        public long ConnectionGeneration => _connection.Recovery.Generation;
        public int FrameOverflowCount => _frames.OverflowCount;
        public void Tick(float deltaTime)
        {
            _connection.Tick(deltaTime);
            if (ConnectionState != RoomGatewayConnectionState.Connected)
                lock (_snapshotGate)
                {
                    _latest = null;
                    _frames.ResumeAfterSnapshot();
                }
        }
        public bool CompleteRestore(long generation) => _connection.Recovery.CompleteRestore(generation);

        public Task<WireRequestFullStateSyncRes> RequestFullSnapshotAsync(
            WireRequestFullStateSyncReq request, CancellationToken cancellationToken) =>
            _connection.RoomClient.RequestFullStateSyncAsync(request, cancellationToken: cancellationToken);

        public Task<WireRoomSubscribeFrameSyncRes> SubscribeFrameSyncAsync(
            WireRoomSubscribeFrameSyncReq request, CancellationToken cancellationToken) =>
            _connection.RoomClient.SubscribeFrameSyncAsync(request, cancellationToken: cancellationToken);

        public Task<WireRoomSubmitFrameInputRes> SubmitFrameInputAsync(
            WireRoomSubmitFrameInputReq request, CancellationToken cancellationToken) =>
            _connection.RoomClient.SubmitFrameInputAsync(request, cancellationToken: cancellationToken);

        public bool TryDequeueFrame(out WireRoomFramePush frame)
        {
            return _frames.TryDequeue(out frame);
        }

        public bool TryGetFrameOverflow(out int minimumRecoveryFrame) =>
            _frames.TryGetOverflow(out minimumRecoveryFrame);

        public void PrepareFullSnapshotRecovery()
        {
            lock (_snapshotGate)
            {
                _latest = null;
                _frames.PauseForRecovery();
            }
        }

        public void ResumeFrameStream() => _frames.ResumeAfterSnapshot();

        public static async Task<TinyGatewayClient> ConnectAsync(
            string host, int port, CancellationToken cancellationToken)
        {
            var connection = await RoomGatewayConnectionSession.ConnectAsync(
                host, port, timeout: TimeSpan.FromSeconds(10), cancellationToken: cancellationToken);
            try
            {
                return new TinyGatewayClient(connection);
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        public bool TryGetLatest(out WireStateSyncSnapshotPush snapshot)
        {
            lock (_snapshotGate)
            {
                if (_latest.HasValue)
                {
                    snapshot = _latest.Value;
                    return true;
                }
            }
            snapshot = default;
            return false;
        }

        public async Task<WireSubmitBattleInputRes> SubmitAsync(
            string sessionToken, string battleId, ulong worldId, uint playerId,
            TinyInput input, ulong sequence, CancellationToken cancellationToken)
        {
            var request = new WireSubmitBattleInputReq
            {
                SessionToken = sessionToken,
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

        private void OnSnapshot(WireStateSyncSnapshotPush snapshot)
        {
            lock (_snapshotGate)
            {
                if (!_latest.HasValue || snapshot.WorldId != _latest.Value.WorldId ||
                    snapshot.Frame > _latest.Value.Frame)
                    _latest = snapshot;
            }
        }

        private void OnFrame(WireRoomFramePush frame)
        {
            lock (_snapshotGate)
            {
                if (!_frames.Enqueue(in frame) && _frames.TryGetOverflow(out _))
                    _latest = null;
            }
        }

        public void Dispose()
        {
            _connection.RoomClient.StateSyncSnapshotReceived -= OnSnapshot;
            _connection.RoomClient.FrameSyncFrameReceived -= OnFrame;
            _connection.Dispose();
        }
    }
}
