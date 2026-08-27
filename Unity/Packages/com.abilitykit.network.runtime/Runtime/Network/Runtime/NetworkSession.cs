using System;
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

    public sealed class NetworkSession : INetworkRuntimeSession, IProtocolRoutedConnection
    {
        private readonly ITransport _transport;
        private readonly IDispatcher _dispatcher;
        private readonly IDispatcher _ioDispatcher;
        private readonly IFrameCodec _frameCodec;
        private readonly IFrameDecoder _frameDecoder;

        private readonly NetworkPipeline _pipeline;
        private readonly NetworkPacketRouter _packetRouter;
        private readonly SessionContext _context;

        private bool _started;

        public NetworkSession(ITransport transport, IDispatcher dispatcher = null, IFrameCodec frameCodec = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _dispatcher = dispatcher ?? InlineDispatcher.Instance;
            _ioDispatcher = _dispatcher;
            _frameCodec = frameCodec ?? LengthPrefixedFrameCodec.Instance;
            _frameDecoder = _frameCodec.CreateDecoder();

            _pipeline = new NetworkPipeline();
            _packetRouter = new NetworkPacketRouter(exception => _dispatcher.Post(() => Error?.Invoke(exception)));
            _context = new SessionContext(this, _dispatcher);
        }

        public NetworkSession(ITransport transport, IDispatcher callbackDispatcher, IDispatcher ioDispatcher, IFrameCodec frameCodec = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _dispatcher = callbackDispatcher ?? InlineDispatcher.Instance;
            _ioDispatcher = ioDispatcher ?? InlineDispatcher.Instance;
            _frameCodec = frameCodec ?? LengthPrefixedFrameCodec.Instance;
            _frameDecoder = _frameCodec.CreateDecoder();

            _pipeline = new NetworkPipeline();
            _packetRouter = new NetworkPacketRouter(exception => _dispatcher.Post(() => Error?.Invoke(exception)));
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

        public void Start()
        {
            if (_started) return;
            _started = true;

            _transport.Connected += OnConnected;
            _transport.Disconnected += OnDisconnected;
            _transport.Error += OnError;
            _transport.BytesReceived += OnBytesReceived;
        }

        public void Stop()
        {
            if (!_started) return;
            _started = false;

            _transport.Connected -= OnConnected;
            _transport.Disconnected -= OnDisconnected;
            _transport.Error -= OnError;
            _transport.BytesReceived -= OnBytesReceived;

            _frameDecoder.Reset();
        }

        public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0)
        {
            if (payload.Array == null) payload = default;

            var header = new NetworkPacketHeader((NetworkPacketFlags)flags, opCode, seq, (uint)payload.Count);
            _pipeline.ProcessOutbound(_context, header, payload, SendRaw);
        }

        public void Dispose()
        {
            Stop();
            _transport.Dispose();
        }

        private void OnConnected()
        {
            _dispatcher.Post(() => Connected?.Invoke());
        }

        private void OnDisconnected()
        {
            _dispatcher.Post(() => Disconnected?.Invoke());
        }

        private void OnError(Exception ex)
        {
            _dispatcher.Post(() => Error?.Invoke(ex));
        }

        private void OnBytesReceived(ArraySegment<byte> bytes)
        {
            if (_ioDispatcher == null)
            {
                HandleBytesReceived(bytes);
                return;
            }

            // Inline fast path: avoid allocating a closure per received chunk when the
            // dispatcher would invoke inline anyway.
            if (ReferenceEquals(_ioDispatcher, InlineDispatcher.Instance))
            {
                HandleBytesReceived(bytes);
                return;
            }

            // ITransport only guarantees receive bytes for the duration of this callback.
            // Own the chunk before crossing the asynchronous dispatcher boundary.
            var copy = Copy(bytes);
            _ioDispatcher.Post(() => HandleBytesReceived(copy));
        }

        private void HandleBytesReceived(ArraySegment<byte> bytes)
        {
            try
            {
                _frameDecoder.Append(bytes);
                while (_frameDecoder.TryRead(out var header, out var payload))
                {
                    _pipeline.ProcessInbound(_context, header, payload, DispatchPacketReceived);
                }
            }
            catch (Exception ex)
            {
                _dispatcher.Post(() => Error?.Invoke(ex));
            }
        }

        private void DispatchPacketReceived(NetworkPacketHeader header, ArraySegment<byte> payload)
        {
            _packetRouter.Dispatch(header, payload);

            var opCode = header.OpCode;
            var seq = header.Seq;

            // Inline fast path: avoid allocating a closure per packet when the dispatcher
            // would invoke inline anyway (single-threaded hosts / smoke runners).
            var inline = ReferenceEquals(_dispatcher, InlineDispatcher.Instance);

            if ((header.Flags & NetworkPacketFlags.ServerPush) != 0)
            {
                if (inline)
                {
                    ServerPushReceived?.Invoke(opCode, payload);
                    return;
                }

                _dispatcher.Post(() => ServerPushReceived?.Invoke(opCode, payload));
                return;
            }

            if (inline)
            {
                PacketReceived?.Invoke(opCode, seq, payload);
                return;
            }

            _dispatcher.Post(() => PacketReceived?.Invoke(opCode, seq, payload));
        }

        private void SendRaw(NetworkPacketHeader header, ArraySegment<byte> payload)
        {
            var frame = _frameCodec.Encode(header, payload);
            _transport.Send(frame);
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

        private sealed class SessionContext : AbilityKit.Network.Abstractions.ISessionContext
        {
            private readonly NetworkSession _session;

            public SessionContext(NetworkSession session, IDispatcher dispatcher)
            {
                _session = session;
                Dispatcher = dispatcher;
            }

            public AbilityKit.Network.Abstractions.ISession Session => _session;

            public IDispatcher Dispatcher { get; }

            public void Send(NetworkPacketHeader header, ArraySegment<byte> payload)
            {
                _session._pipeline.ProcessOutbound(this, header, payload, _session.SendRaw);
            }
        }
    }
}
