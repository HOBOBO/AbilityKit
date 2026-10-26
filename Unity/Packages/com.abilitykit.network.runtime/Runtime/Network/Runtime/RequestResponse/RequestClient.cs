using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Runtime
{
    /// <summary>Correlates request packets with their asynchronous responses.</summary>
    public interface IRequestClient : IDisposable
    {
        Task<ArraySegment<byte>> SendRequestAsync(
            uint opCode,
            ArraySegment<byte> payload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);
    }

    public sealed class RequestClient : IRequestClient
    {
        private readonly IConnection _connection;
        private readonly IRequestResponseEnvelopeCodec _responseCodec;
        private readonly ConcurrentDictionary<uint, PendingRequest> _pending = new();
        private int _nextSeq;
        private bool _disposed;

        public RequestClient(
            IConnection connection,
            IRequestResponseEnvelopeCodec? responseCodec = null)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            // Runtime request correlation is transport/protocol neutral. Gateway envelopes are
            // selected explicitly by NetworkProtocolSession or another protocol adapter.
            _responseCodec = responseCodec ?? RawRequestResponseEnvelopeCodec.Instance;
            _connection.PacketReceived += OnPacketReceived;
            _connection.Disconnected += OnDisconnected;
            _connection.Error += OnError;
        }

        public Task<ArraySegment<byte>> SendRequestAsync(uint opCode, ArraySegment<byte> payload, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var seq = unchecked((uint)Interlocked.Increment(ref _nextSeq));
            if (seq == 0) seq = unchecked((uint)Interlocked.Increment(ref _nextSeq));

            var pending = new PendingRequest(opCode);
            if (!_pending.TryAdd(seq, pending))
            {
                throw new InvalidOperationException($"Duplicate request seq: {seq}");
            }

            CancellationTokenSource? timeoutCts = null;
            CancellationTokenRegistration cancellationRegistration = default;
            CancellationTokenRegistration timeoutRegistration = default;

            try
            {
                if (timeout.HasValue && timeout.Value > TimeSpan.Zero)
                {
                    timeoutCts = new CancellationTokenSource(timeout.Value);
                    timeoutRegistration = timeoutCts.Token.Register(
                        () => TryTimeout(opCode, seq),
                        useSynchronizationContext: false);
                }

                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(
                        () => TryCancel(seq, cancellationToken),
                        useSynchronizationContext: false);
                }

                _connection.Send(opCode, payload, flags: (ushort)NetworkPacketFlags.Request, seq: seq);
                return AwaitAndReleaseAsync(
                    pending.Completion.Task,
                    timeoutCts,
                    timeoutRegistration,
                    cancellationRegistration);
            }
            catch
            {
                _pending.TryRemove(seq, out _);
                cancellationRegistration.Dispose();
                timeoutRegistration.Dispose();
                timeoutCts?.Dispose();
                throw;
            }
        }

        private static async Task<ArraySegment<byte>> AwaitAndReleaseAsync(
            Task<ArraySegment<byte>> requestTask,
            CancellationTokenSource? timeoutCts,
            CancellationTokenRegistration timeoutRegistration,
            CancellationTokenRegistration cancellationRegistration)
        {
            try
            {
                return await requestTask.ConfigureAwait(false);
            }
            finally
            {
                cancellationRegistration.Dispose();
                timeoutRegistration.Dispose();
                timeoutCts?.Dispose();
            }
        }

        private void OnPacketReceived(uint opCode, uint seq, ArraySegment<byte> payload)
        {
            if (seq == 0) return;

            if (!_pending.TryRemove(seq, out var pending) || pending == null)
            {
                return;
            }

            if (pending.OpCode != opCode)
            {
                pending.Completion.TrySetException(new InvalidOperationException(
                    $"Response opcode mismatch. expected={pending.OpCode} actual={opCode} seq={seq}"));
                return;
            }

            var result = Copy(payload);
            try
            {
                pending.Completion.TrySetResult(_responseCodec.Decode(opCode, seq, result));
            }
            catch (Exception ex)
            {
                pending.Completion.TrySetException(ex);
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

        private void TryTimeout(uint opCode, uint seq)
        {
            if (_pending.TryRemove(seq, out var pending) && pending != null)
            {
                pending.Completion.TrySetException(new TimeoutException($"Request timeout. opCode={opCode} seq={seq}"));
            }
        }

        private void TryCancel(uint seq, CancellationToken cancellationToken)
        {
            if (_pending.TryRemove(seq, out var pending) && pending != null)
            {
                pending.Completion.TrySetCanceled(cancellationToken);
            }
        }

        private void FailAll(Exception ex)
        {
            foreach (var kv in _pending)
            {
                if (_pending.TryRemove(kv.Key, out var pending) && pending != null)
                {
                    pending.Completion.TrySetException(ex);
                }
            }
        }

        private static ArraySegment<byte> Copy(ArraySegment<byte> src)
        {
            if (src.Array == null || src.Count <= 0) return default;
            // Allocates a permanent copy — the underlying transport buffer may be reused/returned before
            // the consumer processes it. ArrayPool can't be used here because there's no return mechanism
            // in the TaskCompletionSource<ArraySegment<byte>> API. Gen0 handles this fine at game frame rates.
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

        private sealed class PendingRequest
        {
            public PendingRequest(uint opCode)
            {
                OpCode = opCode;
                Completion = new TaskCompletionSource<ArraySegment<byte>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public uint OpCode { get; }
            public TaskCompletionSource<ArraySegment<byte>> Completion { get; }
        }
    }
}
