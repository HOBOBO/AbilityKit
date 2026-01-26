using System;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Runtime
{
    public sealed class NetworkSession : ISession
    {
        private readonly ITransport _transport;
        private readonly IDispatcher _dispatcher;
        private readonly IFrameCodec _frameCodec;
        private readonly IFrameDecoder _frameDecoder;

        private readonly NetworkPipeline _pipeline;
        private readonly SessionContext _context;

        private bool _started;

        public NetworkSession(ITransport transport, IDispatcher dispatcher = null, IFrameCodec frameCodec = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _dispatcher = dispatcher ?? InlineDispatcher.Instance;
            _frameCodec = frameCodec ?? LengthPrefixedFrameCodec.Instance;
            _frameDecoder = _frameCodec.CreateDecoder();

            _pipeline = new NetworkPipeline();
            _context = new SessionContext(this, _dispatcher);
        }

        public bool IsConnected => _transport.IsConnected;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<Exception> Error;

        public event Action<uint, uint, ArraySegment<byte>> PacketReceived;

        public NetworkPipeline Pipeline => _pipeline;

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
            var opCode = header.OpCode;
            var seq = header.Seq;
            _dispatcher.Post(() => PacketReceived?.Invoke(opCode, seq, payload));
        }

        private void SendRaw(NetworkPacketHeader header, ArraySegment<byte> payload)
        {
            var frame = _frameCodec.Encode(header, payload);
            _transport.Send(frame);
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
