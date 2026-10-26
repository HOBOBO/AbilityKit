using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Host;

namespace AbilityKit.Network.Host.Kcp
{
    public sealed class KcpChannelListener : IChannelListener
    {
        private readonly object _gate = new object();
        private readonly KcpChannelListenerOptions _options;
        private readonly Dictionary<string, KcpServerChannel> _channels = new(StringComparer.Ordinal);
        private Socket? _socket;
        private CancellationTokenSource? _cts;
        private Task? _receiveLoop;
        private Task? _updateLoop;
        private long _nextId;
        private readonly Stopwatch _clock = new Stopwatch();

        public KcpChannelListener(KcpChannelListenerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (_options.Address == null) throw new ArgumentException("Address is required.", nameof(options));
            if (_options.ConversationId == 0) throw new ArgumentOutOfRangeException(nameof(options), "ConversationId must be positive.");
            if (_options.Port < 0 || _options.Port > 65535) throw new ArgumentOutOfRangeException(nameof(options));
            if (_options.ReceiveBufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(options));
            if (_options.UpdateIntervalMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(options));
        }

        public bool IsListening { get { lock (_gate) return _socket != null; } }
        public string Endpoint { get; private set; } = string.Empty;
        public event Action<IServerChannel>? ChannelAccepted;
        public event Action<Exception>? Error;

        public void Start()
        {
            lock (_gate)
            {
                if (_socket != null) throw new InvalidOperationException("Listener is already running.");
                var socket = new Socket(_options.Address.AddressFamily, SocketType.Dgram, ProtocolType.Udp)
                {
                    ReceiveBufferSize = _options.ReceiveBufferSize,
                    SendBufferSize = _options.ReceiveBufferSize
                };
                socket.Bind(new IPEndPoint(_options.Address, _options.Port));
                _socket = socket;
                _cts = new CancellationTokenSource();
                _clock.Restart();
                Endpoint = ((IPEndPoint)socket.LocalEndPoint!).ToString();
                var cancellationToken = _cts.Token;
                _receiveLoop = Task.Run(() => ReceiveLoopAsync(cancellationToken));
                _updateLoop = Task.Run(() => UpdateLoopAsync(cancellationToken));
            }
        }

        public void Stop()
        {
            Socket socket;
            CancellationTokenSource cts;
            lock (_gate)
            {
                socket = _socket;
                cts = _cts;
                _socket = null;
                _cts = null;
                _channels.Clear();
            }
            try { cts?.Cancel(); } catch { }
            try { socket?.Close(); } catch { }
            try { socket?.Dispose(); } catch { }
        }

        public void Dispose() => Stop();

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[_options.ReceiveBufferSize];
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Socket socket;
                    lock (_gate) socket = _socket;
                    if (socket == null) break;
                    EndPoint remote = new IPEndPoint(_options.Address.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
                    var count = await Task.Run(() => socket.ReceiveFrom(buffer, ref remote), cancellationToken).ConfigureAwait(false);
                    if (count <= 0 || remote is not IPEndPoint endpoint) continue;
                    var datagram = new byte[count];
                    Buffer.BlockCopy(buffer, 0, datagram, 0, count);
                    if (!IsValidInitialDatagram(datagram)) continue;
                    var key = endpoint.ToString();
                    KcpServerChannel channel;
                    var accepted = false;
                    lock (_gate)
                    {
                        if (!ReferenceEquals(_socket, socket) || cancellationToken.IsCancellationRequested) continue;
                        if (!_channels.TryGetValue(key, out channel))
                        {
                            channel = new KcpServerChannel(
                                Interlocked.Increment(ref _nextId).ToString(),
                                socket,
                                endpoint,
                                _options.ConversationId,
                                OnChannelClosed);
                            _channels.Add(key, channel);
                            accepted = true;
                        }
                    }
                    if (accepted)
                    {
                        try { ChannelAccepted?.Invoke(channel); }
                        catch (Exception ex) { PublishError(ex); channel.Dispose(); continue; }
                    }
                    channel.AcceptDatagram(datagram, datagram.Length);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception ex) { PublishError(ex); }
        }

        private async Task UpdateLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    KcpServerChannel[] channels;
                    lock (_gate)
                    {
                        channels = new KcpServerChannel[_channels.Count];
                        _channels.Values.CopyTo(channels, 0);
                    }
                    var now = unchecked((uint)_clock.ElapsedMilliseconds);
                    foreach (var channel in channels) channel.Update(now);
                    await Task.Delay(_options.UpdateIntervalMilliseconds, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { PublishError(ex); }
        }

        private void OnChannelClosed(KcpServerChannel channel)
        {
            lock (_gate)
            {
                var key = channel.RemoteEndpoint;
                if (_channels.TryGetValue(key, out var current) && ReferenceEquals(current, channel))
                    _channels.Remove(key);
            }
        }

        private bool IsValidInitialDatagram(byte[] bytes)
        {
            return bytes.Length >= 24 && BitConverter.ToUInt32(bytes, 0) == _options.ConversationId;
        }

        private void PublishError(Exception exception)
        {
            try { Error?.Invoke(exception); } catch { }
        }
    }
}
