using System;
using System.Collections.Generic;
using System.Threading;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Network.Host.InProcess
{
    /// <summary>
    /// Queue-free in-process listener for a local host client. It is a production
    /// composition option, while latency/loss simulation remains middleware-owned.
    /// </summary>
    public sealed class InProcessChannelListener : IChannelListener
    {
        private readonly object _gate = new object();
        private readonly HashSet<InProcessServerChannel> _channels = new HashSet<InProcessServerChannel>();
        private long _nextId;
        private bool _disposed;

        public bool IsListening { get; private set; }
        public string Endpoint => "inprocess";

        public event Action<IServerChannel> ChannelAccepted;
        public event Action<Exception> Error;

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(InProcessChannelListener));
            if (IsListening) throw new InvalidOperationException("Listener is already running.");
            IsListening = true;
        }

        public void Stop()
        {
            IsListening = false;
        }

        private void CloseChannels()
        {
            InProcessServerChannel[] channels;
            lock (_gate)
            {
                channels = new InProcessServerChannel[_channels.Count];
                _channels.CopyTo(channels);
                _channels.Clear();
            }
            foreach (var channel in channels) channel.Dispose();
        }

        public ITransport CreateClientTransport()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(InProcessChannelListener));
            if (!IsListening) throw new InvalidOperationException("Start the listener before creating a client transport.");

            var id = Interlocked.Increment(ref _nextId).ToString();
            var link = new InProcessLink();
            var server = new InProcessServerChannel(id, link);
            var client = new InProcessClientTransport(link);
            server.Closed += OnChannelClosed;
            lock (_gate) _channels.Add(server);

            try
            {
                ChannelAccepted?.Invoke(server);
                return client;
            }
            catch (Exception ex)
            {
                server.Dispose();
                Error?.Invoke(ex);
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            CloseChannels();
            _disposed = true;
        }

        private void OnChannelClosed(IServerChannel channel)
        {
            lock (_gate)
            {
                if (channel is InProcessServerChannel concrete) _channels.Remove(concrete);
            }
        }

        private sealed class InProcessLink
        {
            private int _closed;
            public bool IsOpen => Volatile.Read(ref _closed) == 0;
            public event Action<ArraySegment<byte>> ClientBytes;
            public event Action<ArraySegment<byte>> ServerBytes;
            public event Action Closed;

            public void SendToClient(ArraySegment<byte> bytes)
            {
                if (!IsOpen) throw new InvalidOperationException("In-process link is closed.");
                ClientBytes?.Invoke(Copy(bytes));
            }

            public void SendToServer(ArraySegment<byte> bytes)
            {
                if (!IsOpen) throw new InvalidOperationException("In-process link is closed.");
                ServerBytes?.Invoke(Copy(bytes));
            }

            public void Close()
            {
                if (Interlocked.Exchange(ref _closed, 1) != 0) return;
                Closed?.Invoke();
            }

            private static ArraySegment<byte> Copy(ArraySegment<byte> bytes)
            {
                if (bytes.Array == null || bytes.Count == 0) return default;
                var copy = new byte[bytes.Count];
                Buffer.BlockCopy(bytes.Array, bytes.Offset, copy, 0, bytes.Count);
                return new ArraySegment<byte>(copy);
            }
        }

        private sealed class InProcessServerChannel : IServerChannel
        {
            private readonly InProcessLink _link;

            public InProcessServerChannel(string id, InProcessLink link)
            {
                Id = id;
                _link = link;
                _link.ServerBytes += OnBytes;
                _link.Closed += OnClosed;
            }

            public string Id { get; }
            public string RemoteEndpoint => "inprocess-client";
            public bool IsConnected => _link.IsOpen;
            public event Action<ArraySegment<byte>> BytesReceived;
            public event Action<IServerChannel> Closed;
            public event Action<Exception> Error;

            public void Send(ArraySegment<byte> bytes)
            {
                try { _link.SendToClient(bytes); }
                catch (Exception ex) { Error?.Invoke(ex); throw; }
            }

            public void Close() => _link.Close();

            public void Dispose()
            {
                _link.ServerBytes -= OnBytes;
                _link.Closed -= OnClosed;
                _link.Close();
            }

            private void OnBytes(ArraySegment<byte> bytes) => BytesReceived?.Invoke(bytes);
            private void OnClosed() => Closed?.Invoke(this);
        }

        private sealed class InProcessClientTransport : ITransport
        {
            private readonly InProcessLink _link;
            private bool _connected;

            public InProcessClientTransport(InProcessLink link)
            {
                _link = link;
                _link.ClientBytes += OnBytes;
                _link.Closed += OnClosed;
            }

            public bool IsConnected => _connected && _link.IsOpen;
            public event Action Connected;
            public event Action Disconnected;
            public event Action<Exception> Error;
            public event Action<ArraySegment<byte>> BytesReceived;

            public void Connect(string host, int port)
            {
                if (_connected) throw new InvalidOperationException("Transport is already connected.");
                if (!_link.IsOpen) throw new InvalidOperationException("In-process host channel is closed.");
                _connected = true;
                Connected?.Invoke();
            }

            public void Send(ArraySegment<byte> bytes)
            {
                try { _link.SendToServer(bytes); }
                catch (Exception ex) { Error?.Invoke(ex); throw; }
            }

            public void Close() => _link.Close();

            public void Dispose()
            {
                _link.ClientBytes -= OnBytes;
                _link.Closed -= OnClosed;
                _link.Close();
            }

            private void OnBytes(ArraySegment<byte> bytes) => BytesReceived?.Invoke(bytes);

            private void OnClosed()
            {
                if (!_connected) return;
                _connected = false;
                Disconnected?.Invoke();
            }
        }
    }
}
