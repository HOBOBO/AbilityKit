using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Network.Host.Tcp
{
    public sealed class TcpServerChannel : IServerChannel
    {
        private readonly object _gate = new object();
        private readonly object _sendGate = new object();
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly int _receiveBufferSize;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private int _closed;

        internal TcpServerChannel(string id, TcpClient client, int receiveBufferSize)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Channel id is required.", nameof(id));
            if (receiveBufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(receiveBufferSize));
            Id = id;
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _stream = client.GetStream();
            _receiveBufferSize = receiveBufferSize;
            RemoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? string.Empty;
        }

        public string Id { get; }
        public string RemoteEndpoint { get; }
        public bool IsConnected => Volatile.Read(ref _closed) == 0 && _client.Connected;

        public event Action<ArraySegment<byte>> BytesReceived;
        public event Action<IServerChannel> Closed;
        public event Action<Exception> Error;

        internal void StartReceive()
        {
            if (Volatile.Read(ref _closed) != 0) return;
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        }

        public void Send(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null || bytes.Count == 0) return;
            if (Volatile.Read(ref _closed) != 0) throw new InvalidOperationException("Channel is closed.");

            lock (_sendGate)
            {
                try
                {
                    _stream.Write(bytes.Array, bytes.Offset, bytes.Count);
                }
                catch (Exception ex)
                {
                    PublishError(ex);
                    Close();
                    throw;
                }
            }
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;
            try { _cts.Cancel(); } catch { }
            try { _stream.Close(); } catch { }
            try { _client.Close(); } catch { }
            Closed?.Invoke(this);
        }

        public void Dispose()
        {
            Close();
            _cts.Dispose();
            _stream.Dispose();
            _client.Dispose();
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[_receiveBufferSize];
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var count = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (count == 0) break;

                    // Event consumers may dispatch asynchronously, so each delivery owns its bytes.
                    var delivery = new byte[count];
                    Buffer.BlockCopy(buffer, 0, delivery, 0, count);
                    BytesReceived?.Invoke(new ArraySegment<byte>(delivery));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                PublishError(ex);
            }
            finally
            {
                Close();
            }
        }

        private void PublishError(Exception exception)
        {
            try { Error?.Invoke(exception); } catch { }
        }
    }
}
