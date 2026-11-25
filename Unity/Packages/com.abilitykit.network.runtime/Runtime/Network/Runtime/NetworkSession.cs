using System;
using System.Threading;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Runtime
{
    /// <summary>
    /// A transport session that exposes the middleware pipeline used by the connection runtime.
    /// Implementations returned by a connection session factory are owned and disposed by the
    /// connection manager, and therefore own the transport supplied to that factory.
    /// </summary>
    public interface INetworkRuntimeSession : ISession
    {
        NetworkPipeline Pipeline { get; }
        NetworkPacketRouter PacketRouter { get; }
    }

    /// <summary>Optional transport counters exposed by sessions that collect I/O diagnostics.</summary>
    public interface INetworkRuntimeSessionDiagnostics
    {
        long BytesReceivedCount { get; }
        long BytesSentCount { get; }
        long PacketsReceivedCount { get; }
        long PacketsSentCount { get; }
    }

    /// <summary>
    /// 拥有传输的成帧会话；每次启动使用独立解码器和回调周期。
    /// </summary>
    public sealed class NetworkSession : INetworkRuntimeSession, INetworkRuntimeSessionDiagnostics, IProtocolRoutedConnection
    {
        private readonly ITransport _transport;
        private readonly IDispatcher _dispatcher;
        private readonly IDispatcher _ioDispatcher;
        private readonly IFrameCodec _frameCodec;
        private readonly int _maxFrameLength;
        private IFrameDecoder _initialDecoder;

        private readonly NetworkPipeline _pipeline;
        private readonly NetworkPacketRouter _packetRouter;
        private readonly SessionContext _context;
        private long _bytesReceived;
        private long _bytesSent;
        private long _packetsReceived;
        private long _packetsSent;

        private ReceiveCycle _receiveCycle;
        private int _disposed;

        /// <summary>
        /// 创建会话，接收解码和用户回调共用指定调度器。
        /// </summary>
        public NetworkSession(
            ITransport transport,
            IDispatcher dispatcher = null,
            IFrameCodec frameCodec = null,
            ProtocolPacketBoundaryValidator boundaryValidator = null,
            int maxFrameLength = 4 * 1024 * 1024,
            bool validatePacketHeaders = false)
            : this(transport, dispatcher, dispatcher, frameCodec, boundaryValidator, maxFrameLength, validatePacketHeaders)
        {
        }

        /// <summary>
        /// 创建会话，允许分别指定串行 I/O 调度器与用户回调调度器。
        /// </summary>
        public NetworkSession(
            ITransport transport,
            IDispatcher callbackDispatcher,
            IDispatcher ioDispatcher,
            IFrameCodec frameCodec = null,
            ProtocolPacketBoundaryValidator boundaryValidator = null,
            int maxFrameLength = 4 * 1024 * 1024,
            bool validatePacketHeaders = false)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _dispatcher = callbackDispatcher ?? InlineDispatcher.Instance;
            _ioDispatcher = ioDispatcher ?? InlineDispatcher.Instance;
            _frameCodec = frameCodec ?? LengthPrefixedFrameCodec.Instance;
            _maxFrameLength = maxFrameLength;
            _initialDecoder = CreateDecoder();

            _pipeline = new NetworkPipeline();
            _packetRouter = new NetworkPacketRouter(
                exception => Volatile.Read(ref _receiveCycle)?.OnError(exception),
                boundaryValidator == null ? null : boundaryValidator.Validate,
                validatePacketHeaders ? NetworkPacketHeaderValidation.Validate : null);
            _context = new SessionContext(this, _dispatcher);
        }

        public bool IsConnected => _transport.IsConnected;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<Exception> Error;

        public event Action<uint, uint, ArraySegment<byte>> PacketReceived;
        public event Action<uint, ArraySegment<byte>> ServerPushReceived;

        public NetworkPipeline Pipeline => _pipeline;

        public NetworkPacketRouter PacketRouter => _packetRouter;
        public long BytesReceivedCount => Interlocked.Read(ref _bytesReceived);
        public long BytesSentCount => Interlocked.Read(ref _bytesSent);
        public long PacketsReceivedCount => Interlocked.Read(ref _packetsReceived);
        public long PacketsSentCount => Interlocked.Read(ref _packetsSent);

        /// <summary>
        /// 启动一个新的接收周期；生命周期操作由宿主串行调用，已释放的会话不能重启。
        /// </summary>
        public void Start()
        {
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(NetworkSession));
            if (Volatile.Read(ref _receiveCycle) != null) return;

            var cycle = new ReceiveCycle(this, _initialDecoder ?? CreateDecoder());
            _initialDecoder = null;
            Volatile.Write(ref _receiveCycle, cycle);

            _transport.Connected += cycle.OnConnected;
            _transport.Disconnected += cycle.OnDisconnected;
            _transport.Error += cycle.OnError;
            _transport.BytesReceived += cycle.OnBytesReceived;
        }

        /// <summary>
        /// 撤销当前接收周期。未执行的接收与通知会失效，不等待已经进入的外部回调。
        /// </summary>
        public void Stop()
        {
            var cycle = Interlocked.Exchange(ref _receiveCycle, null);
            if (cycle == null) return;

            _transport.Connected -= cycle.OnConnected;
            _transport.Disconnected -= cycle.OnDisconnected;
            _transport.Error -= cycle.OnError;
            _transport.BytesReceived -= cycle.OnBytesReceived;
        }

        /// <summary>
        /// 通过出站管线发送载荷；已启动周期的延迟出站工作在停止后不再交给传输。
        /// </summary>
        public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0)
        {
            if (payload.Array == null) payload = default;

            var header = new NetworkPacketHeader((NetworkPacketFlags)flags, opCode, seq, (uint)payload.Count);
            var context = Volatile.Read(ref _receiveCycle)?.Context ?? _context;
            context.Send(header, payload);
        }

        /// <summary>
        /// 停止接收并释放本会话持有的传输；重复调用不重复释放传输。
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Stop();
            _transport.Dispose();
        }

        private bool IsCurrent(ReceiveCycle cycle) => ReferenceEquals(Volatile.Read(ref _receiveCycle), cycle);

        private void Post(ReceiveCycle cycle, Action notify)
        {
            if (!IsCurrent(cycle)) return;
            _dispatcher.Post(() =>
            {
                if (IsCurrent(cycle)) notify();
            });
        }

        private void OnBytesReceived(ReceiveCycle cycle, ArraySegment<byte> bytes)
        {
            if (!IsCurrent(cycle)) return;

            if (ReferenceEquals(_ioDispatcher, InlineDispatcher.Instance))
            {
                HandleBytesReceived(cycle, bytes);
                return;
            }

            /*
             * 跨调度器时先复制借用的传输缓冲区；执行时再次核对接收周期。
             */
            var copy = Copy(bytes);
            _ioDispatcher.Post(() => HandleBytesReceived(cycle, copy));
        }

        private void HandleBytesReceived(ReceiveCycle cycle, ArraySegment<byte> bytes)
        {
            if (!IsCurrent(cycle)) return;
            try
            {
                Interlocked.Add(ref _bytesReceived, bytes.Count);
                cycle.Decoder.Append(bytes);
                while (IsCurrent(cycle) && cycle.Decoder.TryRead(out var header, out var payload))
                {
                    Interlocked.Increment(ref _packetsReceived);
                    _pipeline.ProcessInbound(cycle.Context, header, payload, cycle.Dispatch);
                }
            }
            catch (Exception ex)
            {
                cycle.OnError(ex);
            }
        }

        private void DispatchPacketReceived(ReceiveCycle cycle, NetworkPacketHeader header, ArraySegment<byte> payload)
        {
            if (!IsCurrent(cycle)) return;
            var outcome = _packetRouter.DispatchDetailed(header, payload, cycle.ReportError);
            if (outcome == NetworkPacketDispatchOutcome.BoundaryRejected || !IsCurrent(cycle))
                return;

            var opCode = header.OpCode;
            var seq = header.Seq;

            var inline = ReferenceEquals(_dispatcher, InlineDispatcher.Instance);

            if ((header.Flags & NetworkPacketFlags.ServerPush) != 0)
            {
                if (inline)
                {
                    ServerPushReceived?.Invoke(opCode, payload);
                    return;
                }

                var ownedPush = Copy(payload);
                Post(cycle, () => ServerPushReceived?.Invoke(opCode, ownedPush));
                return;
            }

            if (inline)
            {
                PacketReceived?.Invoke(opCode, seq, payload);
                return;
            }

            var ownedPacket = Copy(payload);
            Post(cycle, () => PacketReceived?.Invoke(opCode, seq, ownedPacket));
        }

        private void SendRaw(NetworkPacketHeader header, ArraySegment<byte> payload)
        {
            var frame = _frameCodec.Encode(header, payload);
            _transport.Send(frame);
            Interlocked.Add(ref _bytesSent, frame.Count);
            Interlocked.Increment(ref _packetsSent);
        }

        private static ArraySegment<byte> Copy(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null || bytes.Count == 0)
            {
                return default;
            }

            var copy = new byte[bytes.Count];
            Buffer.BlockCopy(bytes.Array, bytes.Offset, copy, 0, bytes.Count);
            return new ArraySegment<byte>(copy);
        }

        private IFrameDecoder CreateDecoder()
        {
            if (_maxFrameLength <= 0) throw new ArgumentOutOfRangeException("maxFrameLength");
            var decoder = _frameCodec.CreateDecoder();
            if (decoder is IFrameDecoderLimits limits) limits.MaxFrameLength = _maxFrameLength;
            return decoder;
        }

        private sealed class ReceiveCycle
        {
            private readonly NetworkSession _session;

            public ReceiveCycle(NetworkSession session, IFrameDecoder decoder)
            {
                _session = session;
                Decoder = decoder;
                Context = new SessionContext(session, session._dispatcher, this);
                Dispatch = (header, payload) => session.DispatchPacketReceived(this, header, payload);
                ReportError = OnError;
            }

            public IFrameDecoder Decoder { get; }
            public SessionContext Context { get; }
            public Action<NetworkPacketHeader, ArraySegment<byte>> Dispatch { get; }
            public Action<Exception> ReportError { get; }

            public void OnConnected() => _session.Post(this, () => _session.Connected?.Invoke());
            public void OnDisconnected() => _session.Post(this, () => _session.Disconnected?.Invoke());
            public void OnError(Exception error) => _session.Post(this, () => _session.Error?.Invoke(error));
            public void OnBytesReceived(ArraySegment<byte> bytes) => _session.OnBytesReceived(this, bytes);
        }

        private sealed class SessionContext : AbilityKit.Network.Abstractions.ISessionContext
        {
            private readonly NetworkSession _session;
            private readonly ReceiveCycle _cycle;
            private readonly Action<NetworkPacketHeader, ArraySegment<byte>> _sendTerminal;

            public SessionContext(NetworkSession session, IDispatcher dispatcher, ReceiveCycle cycle = null)
            {
                _session = session;
                _cycle = cycle;
                _sendTerminal = SendIfCurrent;
                Dispatcher = dispatcher;
            }

            public AbilityKit.Network.Abstractions.ISession Session => _session;

            public IDispatcher Dispatcher { get; }

            public void Send(NetworkPacketHeader header, ArraySegment<byte> payload)
            {
                if (_cycle == null || _session.IsCurrent(_cycle))
                    _session._pipeline.ProcessOutbound(this, header, payload, _sendTerminal);
            }

            private void SendIfCurrent(NetworkPacketHeader header, ArraySegment<byte> payload)
            {
                if (_cycle == null || _session.IsCurrent(_cycle))
                    _session.SendRaw(header, payload);
            }
        }
    }
}
