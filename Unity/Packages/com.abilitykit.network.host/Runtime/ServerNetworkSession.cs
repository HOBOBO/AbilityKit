using System;
using System.Threading;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Network.Host
{
    /// <summary>
    /// Transport-neutral server-side framed session. It deliberately reuses the
    /// client runtime codec and middleware contracts so both ends share one wire protocol.
    /// </summary>
    public sealed class ServerNetworkSession : IServerNetworkSession, ISession
    {
        private readonly IServerChannel _channel;
        private readonly IDispatcher _dispatcher;
        private readonly IDispatcher _ioDispatcher;
        private readonly IFrameCodec _frameCodec;
        private readonly IFrameDecoder _decoder;
        private readonly IMonotonicClock _clock;
        private readonly NetworkPipeline _pipeline = new NetworkPipeline();
        private readonly SessionContext _context;
        private readonly ServerSessionContext _serverContext = new ServerSessionContext();
        private bool _started;
        private bool _disposed;
        private readonly long _openedTimestamp;
        private long _lastActivityTimestamp;
        private long _bytesReceived;
        private long _bytesSent;
        private long _packetsReceived;
        private long _packetsSent;

        public ServerNetworkSession(
            IServerChannel channel,
            IDispatcher callbackDispatcher = null,
            IDispatcher ioDispatcher = null,
            IFrameCodec frameCodec = null,
            IMonotonicClock clock = null)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _dispatcher = callbackDispatcher ?? InlineDispatcher.Instance;
            _ioDispatcher = ioDispatcher ?? InlineDispatcher.Instance;
            _frameCodec = frameCodec ?? LengthPrefixedFrameCodec.Instance;
            _clock = clock ?? StopwatchMonotonicClock.Instance;
            _decoder = _frameCodec.CreateDecoder();
            _context = new SessionContext(this, _dispatcher);
            _openedTimestamp = _clock.Timestamp;
            _lastActivityTimestamp = _openedTimestamp;
        }

        public string Id => _channel.Id;
        public IServerChannel Channel => _channel;
        public ServerSessionContext Context => _serverContext;
        public bool IsConnected => !_disposed && _channel.IsConnected;
        public long OpenedTimestamp => _openedTimestamp;
        public long LastActivityTimestamp => Interlocked.Read(ref _lastActivityTimestamp);
        public long BytesReceivedCount => Interlocked.Read(ref _bytesReceived);
        public long BytesSentCount => Interlocked.Read(ref _bytesSent);
        public long PacketsReceivedCount => Interlocked.Read(ref _packetsReceived);
        public long PacketsSentCount => Interlocked.Read(ref _packetsSent);
        public NetworkPipeline Pipeline => _pipeline;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<Exception> Error;
        public event Action<uint, uint, ArraySegment<byte>> PacketReceived;
        public event Action<uint, ArraySegment<byte>> ServerPushReceived;
        public event Action<IServerNetworkSession, NetworkPacketHeader, ArraySegment<byte>> RequestReceived;
        event Action<IServerNetworkSession, NetworkPacketHeader, ArraySegment<byte>> IServerNetworkSession.PacketReceived
        {
            add => RequestReceived += value;
            remove => RequestReceived -= value;
        }
        public event Action<IServerNetworkSession> Closed;
        public event Action<IServerNetworkSession, Exception> SessionError;
        event Action<IServerNetworkSession, Exception> IServerNetworkSession.Error
        {
            add => SessionError += value;
            remove => SessionError -= value;
        }

        public void Start()
        {
            ThrowIfDisposed();
            if (_started) return;
            _started = true;
            _channel.BytesReceived += OnBytesReceived;
            _channel.Closed += OnClosed;
            _channel.Error += OnError;
            Connected?.Invoke();
        }

        public void Stop()
        {
            if (!_started) return;
            _started = false;
            _channel.BytesReceived -= OnBytesReceived;
            _channel.Closed -= OnClosed;
            _channel.Error -= OnError;
            _decoder.Reset();
        }

        public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0)
        {
            Send(new NetworkPacketHeader((NetworkPacketFlags)flags, opCode, seq, (uint)payload.Count), payload);
        }

        public void Send(NetworkPacketHeader header, ArraySegment<byte> payload)
        {
            ThrowIfDisposed();
            _pipeline.ProcessOutbound(_context, header, Normalize(payload), SendRaw);
        }

        public void SendResponse(uint opCode, uint seq, ArraySegment<byte> payload)
        {
            Send(new NetworkPacketHeader(NetworkPacketFlags.Response, opCode, seq, (uint)payload.Count), payload);
        }

        public void SendPush(uint opCode, ArraySegment<byte> payload)
        {
            Send(new NetworkPacketHeader(NetworkPacketFlags.ServerPush, opCode, 0, (uint)payload.Count), payload);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _channel.Dispose();
        }

        public void Close()
        {
            if (_disposed) return;
            _channel.Close();
        }

        private void OnBytesReceived(ArraySegment<byte> bytes)
        {
            Interlocked.Add(ref _bytesReceived, bytes.Count);
            MarkActivity();
            if (ReferenceEquals(_ioDispatcher, InlineDispatcher.Instance))
            {
                Decode(bytes);
                return;
            }

            var copy = Copy(bytes);
            _ioDispatcher.Post(() => Decode(copy));
        }

        private void Decode(ArraySegment<byte> bytes)
        {
            try
            {
                _decoder.Append(bytes);
                while (_decoder.TryRead(out var header, out var payload))
                {
                    Interlocked.Increment(ref _packetsReceived);
                    _pipeline.ProcessInbound(_context, header, payload, DispatchPacket);
                }
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }

        private void DispatchPacket(NetworkPacketHeader header, ArraySegment<byte> payload)
        {
            _dispatcher.Post(() =>
            {
                PacketReceived?.Invoke(header.OpCode, header.Seq, payload);
                if ((header.Flags & NetworkPacketFlags.ServerPush) != 0)
                {
                    ServerPushReceived?.Invoke(header.OpCode, payload);
                }
                RequestReceived?.Invoke(this, header, payload);
            });
        }

        private void OnClosed(IServerChannel channel)
        {
            _dispatcher.Post(() =>
            {
                Disconnected?.Invoke();
                Closed?.Invoke(this);
            });
        }

        private void OnError(Exception exception)
        {
            _dispatcher.Post(() =>
            {
                Error?.Invoke(exception);
                SessionError?.Invoke(this, exception);
            });
        }

        private void SendRaw(NetworkPacketHeader header, ArraySegment<byte> payload)
        {
            var frame = _frameCodec.Encode(header, payload);
            _channel.Send(frame);
            Interlocked.Add(ref _bytesSent, frame.Count);
            Interlocked.Increment(ref _packetsSent);
            MarkActivity();
        }

        private void MarkActivity()
        {
            Interlocked.Exchange(ref _lastActivityTimestamp, _clock.Timestamp);
        }

        private static ArraySegment<byte> Normalize(ArraySegment<byte> payload)
        {
            return payload.Array == null ? default : payload;
        }

        private static ArraySegment<byte> Copy(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null || bytes.Count == 0) return default;
            var copy = new byte[bytes.Count];
            Buffer.BlockCopy(bytes.Array, bytes.Offset, copy, 0, bytes.Count);
            return new ArraySegment<byte>(copy);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ServerNetworkSession));
        }

        private sealed class SessionContext : ISessionContext
        {
            private readonly ServerNetworkSession _owner;

            public SessionContext(ServerNetworkSession owner, IDispatcher dispatcher)
            {
                _owner = owner;
                Dispatcher = dispatcher;
            }

            public ISession Session => _owner;
            public IDispatcher Dispatcher { get; }

            public void Send(NetworkPacketHeader header, ArraySegment<byte> payload)
            {
                _owner._pipeline.ProcessOutbound(this, header, payload, _owner.SendRaw);
            }
        }
    }
}
