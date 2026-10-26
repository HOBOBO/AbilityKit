using System;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using ET;

namespace AbilityKit.Network.Transport.Kcp
{
    /// <summary>Runtime tuning for the KCP transport.</summary>
    public sealed class KcpTransportOptions
    {
        public uint ConversationId { get; set; } = 1;
        public int UpdateIntervalMilliseconds { get; set; } = 10;
        public int NoDelay { get; set; } = 1;
        public int FastResend { get; set; } = 2;
        public int NoCongestionWindow { get; set; } = 1;
        public int SendWindowSize { get; set; } = 256;
        public int ReceiveWindowSize { get; set; } = 256;
        public int Mtu { get; set; } = 1200;
        public int MinimumRetransmissionTimeout { get; set; } = 30;
        public int ReceiveBufferSize { get; set; } = 64 * 1024;

        internal void Validate()
        {
            if (ConversationId == 0) throw new ArgumentOutOfRangeException(nameof(ConversationId));
            if (UpdateIntervalMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(UpdateIntervalMilliseconds));
            if (SendWindowSize <= 0) throw new ArgumentOutOfRangeException(nameof(SendWindowSize));
            if (ReceiveWindowSize <= 0) throw new ArgumentOutOfRangeException(nameof(ReceiveWindowSize));
            if (Mtu < 576 || Mtu > 65507) throw new ArgumentOutOfRangeException(nameof(Mtu));
            if (ReceiveBufferSize < Mtu) throw new ArgumentOutOfRangeException(nameof(ReceiveBufferSize));
        }
    }

    /// <summary>
    /// KCP over UDP. The transport carries the same framed AbilityKit payload as TCP;
    /// the server must create a KCP peer with the same conversation id.
    /// </summary>
    public sealed class KcpTransport : ITransport
    {
        private readonly object _gate = new object();
        private readonly object _kcpGate = new object();
        private readonly KcpTransportOptions _options;
        private readonly Stopwatch _clock = new Stopwatch();
        private Socket _socket;
        private ET.Kcp _kcp;
        private CancellationTokenSource _cts;
        private Task _receiveLoop;
        private Task _updateLoop;
        private int _connected;
        private int _closed;

        public KcpTransport(KcpTransportOptions options = null)
        {
            _options = options ?? new KcpTransportOptions();
            _options.Validate();
        }

        public bool IsConnected => Volatile.Read(ref _connected) != 0;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<Exception> Error;
        public event Action<ArraySegment<byte>> BytesReceived;

        public void Connect(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));

            lock (_gate)
            {
                if (_socket != null) throw new InvalidOperationException("Transport already started.");

                var address = ResolveAddress(host);
                var socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp)
                {
                    SendBufferSize = _options.ReceiveBufferSize,
                    ReceiveBufferSize = _options.ReceiveBufferSize
                };
                socket.Connect(new IPEndPoint(address, port));

                _socket = socket;
                _cts = new CancellationTokenSource();
                _kcp = new ET.Kcp(_options.ConversationId, Output);
                _kcp.SetNoDelay(_options.NoDelay, _options.UpdateIntervalMilliseconds, _options.FastResend, _options.NoCongestionWindow);
                _kcp.SetWindowSize(_options.SendWindowSize, _options.ReceiveWindowSize);
                _kcp.SetMtu(_options.Mtu);
                _kcp.SetMinrto(_options.MinimumRetransmissionTimeout);
                _clock.Restart();
                Volatile.Write(ref _closed, 0);
                Volatile.Write(ref _connected, 1);
                _receiveLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token));
                _updateLoop = Task.Run(() => UpdateLoopAsync(_cts.Token));
            }

            Connected?.Invoke();
        }

        public void Send(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null || bytes.Count == 0) return;
            lock (_kcpGate)
            {
                var kcp = _kcp;
                if (kcp == null || !IsConnected) throw new InvalidOperationException("Not connected.");
                if (kcp.Send(bytes) < 0) throw new InvalidOperationException("KCP send queue rejected the payload.");
                kcp.Update(NowMilliseconds());
                kcp.Flush();
            }
        }

        public void Close()
        {
            Socket socket;
            CancellationTokenSource cts;
            ET.Kcp kcp;
            lock (_gate)
            {
                socket = _socket;
                cts = _cts;
                kcp = _kcp;
                _socket = null;
                _cts = null;
                _kcp = null;
                Volatile.Write(ref _connected, 0);
                Volatile.Write(ref _closed, 1);
            }

            try { cts?.Cancel(); } catch { }
            try { socket?.Close(); } catch { }
            try { socket?.Dispose(); } catch { }
            lock (_kcpGate) { kcp?.Dispose(); }
            if (socket != null) Disconnected?.Invoke();
        }

        public void Dispose() => Close();

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
                    var count = await Task.Run(() => socket.Receive(buffer), cancellationToken).ConfigureAwait(false);
                    if (count <= 0) break;
                    lock (_kcpGate)
                    {
                        if (_kcp == null) break;
                        _kcp.Input(buffer, 0, count);
                        DrainReceived();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception ex) { PublishError(ex); }
            finally
            {
                if (!cancellationToken.IsCancellationRequested) Close();
            }
        }

        private async Task UpdateLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    lock (_kcpGate) _kcp?.Update(NowMilliseconds());
                    await Task.Delay(_options.UpdateIntervalMilliseconds, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { PublishError(ex); Close(); }
        }

        private void Output(byte[] bytes, int count)
        {
            if (count <= 0 || !IsConnected) return;
            Socket socket;
            lock (_gate) socket = _socket;
            if (socket == null) return;
            try { socket.Send(bytes, checked((int)ET.KCPBASIC.REVERSED_HEAD), count, SocketFlags.None); }
            catch (Exception ex) { PublishError(ex); Close(); }
        }

        private void DrainReceived()
        {
            while (_kcp != null)
            {
                var size = _kcp.PeekSize();
                if (size <= 0) return;
                var payload = new byte[size];
                var count = _kcp.Receive(payload, size);
                if (count <= 0) return;
                BytesReceived?.Invoke(new ArraySegment<byte>(payload, 0, count));
            }
        }

        private void PublishError(Exception exception)
        {
            try { Error?.Invoke(exception); } catch { }
        }

        private static IPAddress ResolveAddress(string host)
        {
            if (IPAddress.TryParse(host, out var address)) return address;
            var addresses = Dns.GetHostAddresses(host);
            if (addresses.Length == 0) throw new InvalidOperationException("Host did not resolve to an address.");
            return addresses[0];
        }

        private uint NowMilliseconds() => unchecked((uint)_clock.ElapsedMilliseconds);
    }
}
