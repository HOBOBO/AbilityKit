using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.TcpGateway;

namespace AbilityKit.Network.Runtime
{
    public sealed class RequestClient : IDisposable
    {
        private readonly IConnection _connection;
        private readonly ConcurrentDictionary<uint, TaskCompletionSource<ArraySegment<byte>>> _pending = new();
        private int _nextSeq;
        private bool _disposed;

        public RequestClient(IConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _connection.PacketReceived += OnPacketReceived;
            _connection.Disconnected += OnDisconnected;
            _connection.Error += OnError;
        }

        public Task<ArraySegment<byte>> SendRequestAsync(uint opCode, ArraySegment<byte> payload, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var seq = unchecked((uint)Interlocked.Increment(ref _nextSeq));
            if (seq == 0) seq = unchecked((uint)Interlocked.Increment(ref _nextSeq));

            var tcs = new TaskCompletionSource<ArraySegment<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_pending.TryAdd(seq, tcs))
            {
                throw new InvalidOperationException($"Duplicate request seq: {seq}");
            }

            CancellationTokenSource? timeoutCts = null;
            CancellationTokenRegistration ctr = default;
            CancellationTokenRegistration ttr = default;

            try
            {
                if (timeout.HasValue && timeout.Value > TimeSpan.Zero)
                {
                    timeoutCts = new CancellationTokenSource(timeout.Value);
                    ttr = timeoutCts.Token.Register(() => TryTimeout(seq), useSynchronizationContext: false);
                }

                if (cancellationToken.CanBeCanceled)
                {
                    ctr = cancellationToken.Register(() => TryCancel(seq), useSynchronizationContext: false);
                }

                _connection.Send(opCode, payload, flags: (ushort)NetworkPacketFlags.Request, seq: seq);
                return tcs.Task;
            }
            catch
            {
                _pending.TryRemove(seq, out _);
                ctr.Dispose();
                ttr.Dispose();
                timeoutCts?.Dispose();
                throw;
            }
        }

        private void OnPacketReceived(uint opCode, uint seq, ArraySegment<byte> payload)
        {
            if (seq == 0) return;

            if (!_pending.TryRemove(seq, out var tcs) || tcs == null)
            {
                return;
            }

            var result = Copy(payload);
            try
            {
                var decoded = TcpGatewayResponseCodec.Decode(result);
                if (decoded.StatusCode != TcpGatewayStatusCode.Ok)
                {
                    tcs.TrySetException(new InvalidOperationException($"Gateway response error. statusCode={decoded.StatusCode} opCode={opCode} seq={seq}"));
                    return;
                }

                tcs.TrySetResult(decoded.Payload);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        private void OnDisconnected()
        {
            FailAll(new InvalidOperationException("Connection disconnected."));
        }

        private void OnError(Exception ex)
        {
            FailAll(ex ?? new InvalidOperationException("Connection error."));
        }

        private void TryTimeout(uint seq)
        {
            if (_pending.TryRemove(seq, out var tcs) && tcs != null)
            {
                tcs.TrySetException(new TimeoutException($"Request timeout. seq={seq}"));
            }
        }

        private void TryCancel(uint seq)
        {
            if (_pending.TryRemove(seq, out var tcs) && tcs != null)
            {
                tcs.TrySetCanceled();
            }
        }

        private void FailAll(Exception ex)
        {
            foreach (var kv in _pending)
            {
                if (_pending.TryRemove(kv.Key, out var tcs) && tcs != null)
                {
                    tcs.TrySetException(ex);
                }
            }
        }

        private static ArraySegment<byte> Copy(ArraySegment<byte> src)
        {
            if (src.Array == null || src.Count <= 0) return default;
            var bytes = new byte[src.Count];
            Buffer.BlockCopy(src.Array, src.Offset, bytes, 0, src.Count);
            return new ArraySegment<byte>(bytes);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RequestClient));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _connection.PacketReceived -= OnPacketReceived;
            _connection.Disconnected -= OnDisconnected;
            _connection.Error -= OnError;

            FailAll(new ObjectDisposedException(nameof(RequestClient)));
            _pending.Clear();
        }
    }
}
