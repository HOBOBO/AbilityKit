#nullable enable

using System;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// 先通过共享网络中间件管线处理 Shooter 的权威快照推送，然后再分发给客户端同步控制器。
    /// </summary>
    public sealed class ShooterCarrierNetworkLink : IDisposable
    {
        private readonly IShooterClientSyncController _controller;
        private readonly NetworkConditioningMiddleware _conditioning;
        private readonly NetworkPipeline _pipeline;
        private readonly LoopbackSessionContext _context = new LoopbackSessionContext();
        private readonly ReusableMemoryPackSerializationBuffer _pureStatePayloadBuffer = new ReusableMemoryPackSerializationBuffer();
        private readonly ReusableMemoryPackSerializationBuffer _wirePayloadBuffer = new ReusableMemoryPackSerializationBuffer();

        private uint _sequence;
        private long _clockMs;
        private ShooterSnapshotApplyResult _lastApplyResult = ShooterSnapshotApplyResult.Ignored;

        public ShooterCarrierNetworkLink(
            IShooterClientSyncController controller,
            NetworkConditionProfile profile,
            int seed = 0)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _conditioning = new NetworkConditioningMiddleware(profile, () => _clockMs, seed);
            _pipeline = new NetworkPipeline();
            _pipeline.Add(_conditioning);
        }

        public NetworkConditioningStats Stats => _conditioning.GetStats();

        public ShooterSnapshotApplyResult LastApplyResult => _lastApplyResult;

        public void PublishSnapshot(in ShooterPackedSnapshotPayload snapshot, double timestamp)
        {
            var isDelta = (snapshot.SnapshotFlags & ShooterPackedSnapshotFlags.Delta) != 0u;
            var payloadOpCode = isDelta
                ? ShooterOpCodes.Snapshot.PackedStateDelta
                : ShooterOpCodes.Snapshot.PackedState;
            var packedBytes = ShooterPackedSnapshotCodec.Serialize(in snapshot);
            PublishPayload(
                snapshot.WorldId,
                snapshot.Frame,
                snapshot.ServerTick,
                timestamp,
                isDelta,
                payloadOpCode,
                packedBytes);
        }

        public void PublishSnapshot(in ShooterPureStateSnapshotPayload snapshot, double timestamp)
        {
            var isDelta = snapshot.SnapshotKind != ShooterPureStateSnapshotKinds.FullBaseline;
            var payloadOpCode = isDelta
                ? ShooterOpCodes.Snapshot.PureStateDelta
                : ShooterOpCodes.Snapshot.PureState;
            var payload = ShooterPureStateSyncCodec.SerializeTransientSegment(in snapshot, _pureStatePayloadBuffer);
            PublishPayload(
                snapshot.WorldId,
                snapshot.Frame,
                snapshot.ServerTick,
                timestamp,
                isDelta,
                payloadOpCode,
                payload);
        }

        private void PublishPayload(
            ulong worldId,
            int frame,
            long serverTick,
            double timestamp,
            bool isDelta,
            int payloadOpCode,
            byte[] payload)
        {
            PublishPayload(
                worldId,
                frame,
                serverTick,
                timestamp,
                isDelta,
                payloadOpCode,
                new ArraySegment<byte>(payload));
        }

        private void PublishPayload(
            ulong worldId,
            int frame,
            long serverTick,
            double timestamp,
            bool isDelta,
            int payloadOpCode,
            ArraySegment<byte> payload)
        {
            var pushOpCode = isDelta
                ? RoomGatewayOpCodes.DeltaSnapshotPushed
                : RoomGatewayOpCodes.SnapshotPushed;

            var wire = new WireStateSyncSnapshotPush
            {
                WorldId = worldId,
                Frame = frame,
                Timestamp = timestamp,
                IsFullSnapshot = !isDelta,
                Actors = null,
                PayloadOpCode = payloadOpCode,
                ServerTicks = serverTick
            };

            var pushPayload = WireRoomGatewayBinary.SerializeTransient(in wire, payload, _wirePayloadBuffer);
            var header = new NetworkPacketHeader(
                NetworkPacketFlags.None,
                pushOpCode,
                ++_sequence,
                (uint)pushPayload.Count);

            _pipeline.ProcessInbound(_context, header, pushPayload, DeliverToController);
        }

        public void Advance(long clockMs)
        {
            _clockMs = clockMs;
            _conditioning.Advance(clockMs);
        }

        public void Dispose()
        {
            _conditioning.ClearPending();
        }

        private void DeliverToController(NetworkPacketHeader header, ArraySegment<byte> payload)
        {
            _lastApplyResult = _controller.ApplyGatewayPush(header.OpCode, payload);
        }

        private sealed class LoopbackSessionContext : ISessionContext, ISession, IDispatcher
        {
            public ISession Session => this;

            public IDispatcher Dispatcher => this;

            public bool IsConnected => true;

            public event Action? Connected;

            public event Action? Disconnected;

            public event Action<Exception>? Error;

            public event Action<uint, uint, ArraySegment<byte>>? PacketReceived;

            public event Action<uint, ArraySegment<byte>>? ServerPushReceived;

            public void Send(NetworkPacketHeader header, ArraySegment<byte> payload)
            {
                PacketReceived?.Invoke(header.OpCode, header.Seq, payload);
                ServerPushReceived?.Invoke(header.OpCode, payload);
            }

            public void Post(Action action)
            {
                action?.Invoke();
            }

            public void Start()
            {
                Connected?.Invoke();
            }

            public void Stop()
            {
                Disconnected?.Invoke();
            }

            public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0)
            {
                PacketReceived?.Invoke(opCode, seq, payload);
                ServerPushReceived?.Invoke(opCode, payload);
            }

            public void Dispose()
            {
            }
        }
    }
}
