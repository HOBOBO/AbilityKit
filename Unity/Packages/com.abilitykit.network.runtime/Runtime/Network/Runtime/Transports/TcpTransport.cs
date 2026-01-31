using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Network.Runtime
{
    public sealed class TcpTransport : ITransport
    {
        private readonly object _gate = new object();

        private TcpClient _client;
        private NetworkStream _stream;

        private CancellationTokenSource _cts;
        private Task _receiveLoop;

        public bool IsConnected
        {
            get
            {
                var c = _client;
                return c != null && c.Connected;
            }
        }

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
                if (_client != null) throw new InvalidOperationException("Transport already started.");

                _cts = new CancellationTokenSource();
                _client = new TcpClient
                {
                    NoDelay = true
                };

                _receiveLoop = Task.Run(() => RunAsync(host, port, _cts.Token));
            }
        }

        public void Close()
        {
            TcpClient client;
            CancellationTokenSource cts;

            lock (_gate)
            {
                client = _client;
                cts = _cts;
                _client = null;
                _stream = null;
                _cts = null;
                _receiveLoop = null;
            }

            try
            {
                cts?.Cancel();
            }
            catch
            {
            }

            try
            {
                client?.Close();
            }
            catch
            {
            }

            try
            {
                client?.Dispose();
            }
            catch
            {
            }
        }

        public void Send(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null || bytes.Count <= 0) return;

            NetworkStream stream;
            lock (_gate)
            {
                stream = _stream;
            }

            if (stream == null) throw new InvalidOperationException("Not connected.");

            try
            {
                stream.Write(bytes.Array, bytes.Offset, bytes.Count);
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
                Close();
            }
        }

        public void Dispose()
        {
            Close();
        }

        private async Task RunAsync(string host, int port, CancellationToken ct)
        {
            try
            {
                await _client.ConnectAsync(host, port);
                if (ct.IsCancellationRequested) return;

                var stream = _client.GetStream();
                lock (_gate)
                {
                    _stream = stream;
                }

                Connected?.Invoke();

                var buffer = new byte[64 * 1024];
                while (!ct.IsCancellationRequested)
                {
                    var n = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (n <= 0) break;

                    var bytes = new byte[n];
                    Buffer.BlockCopy(buffer, 0, bytes, 0, n);
                    BytesReceived?.Invoke(new ArraySegment<byte>(bytes));
                }

                if (!ct.IsCancellationRequested)
                {
                    Disconnected?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
                Disconnected?.Invoke();
            }
            finally
            {
                Close();
            }
        }
    }
}
