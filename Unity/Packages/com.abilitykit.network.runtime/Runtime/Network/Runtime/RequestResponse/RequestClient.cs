using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Runtime
{
    /// <summary>
    /// 关联请求与异步响应。释放请求客户端不释放底层连接。
    /// </summary>
    public interface IRequestClient : IDisposable
    {
        /// <summary>
        /// 发送请求并等待响应。支持调用方取消及可选超时，返回的响应不依赖传输接收缓冲区。
        /// </summary>
        Task<ArraySegment<byte>> SendRequestAsync(
            uint opCode,
            ArraySegment<byte> payload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 为借用的连接登记请求。响应、断开、错误、取消、超时和释放竞争同一次请求的完成权。
    /// </summary>
    public sealed class RequestClient : IRequestClient
    {
        private readonly IConnection _connection;
        private readonly IRequestResponseEnvelopeCodec _responseCodec;
        private readonly object _pendingGate = new();
        private Dictionary<uint, PendingRequest> _pending = new();
        private uint _nextSeq;
        private bool _disposed;

        /// <summary>
        /// 订阅连接的响应、断开和错误事件；未指定编解码器时保留原始响应载荷。
        /// </summary>
        public RequestClient(
            IConnection connection,
            IRequestResponseEnvelopeCodec? responseCodec = null)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _responseCodec = responseCodec ?? RawRequestResponseEnvelopeCodec.Instance;
            _connection.PacketReceived += OnPacketReceived;
            _connection.Disconnected += OnDisconnected;
            _connection.Error += OnError;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 向连接发送时不持有请求表锁；在释放前已经开始发送的请求不尝试撤回。
        /// </remarks>
        public Task<ArraySegment<byte>> SendRequestAsync(uint opCode, ArraySegment<byte> payload, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            uint seq;
            PendingRequest pending;
            lock (_pendingGate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(RequestClient));
                cancellationToken.ThrowIfCancellationRequested();

                seq = unchecked(++_nextSeq);
                if (seq == 0) seq = unchecked(++_nextSeq);
                pending = new PendingRequest(opCode);
                if (!_pending.TryAdd(seq, pending))
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

                if (IsPending(seq, pending))
                    _connection.Send(opCode, payload, flags: (ushort)NetworkPacketFlags.Request, seq: seq);
                return AwaitAndReleaseAsync(
                    pending.Completion.Task,
                    timeoutCts,
                    timeoutRegistration,
                    cancellationRegistration);
            }
            catch
            {
                if (TakePending(seq) == null)
                {
                    return AwaitAndReleaseAsync(
                        pending.Completion.Task,
                        timeoutCts,
                        timeoutRegistration,
                        cancellationRegistration);
                }

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

            var pending = TakePending(seq);
            if (pending == null)
            {
                return;
            }

            if (pending.OpCode != opCode)
            {
                pending.Completion.TrySetException(new InvalidOperationException(
                    $"Response opcode mismatch. expected={pending.OpCode} actual={opCode} seq={seq}"));
                return;
            }

            try
            {
                pending.Completion.TrySetResult(_responseCodec.Decode(opCode, seq, Copy(payload)));
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
            TakePending(seq)?.Completion.TrySetException(new TimeoutException($"Request timeout. opCode={opCode} seq={seq}"));
        }

        private void TryCancel(uint seq, CancellationToken cancellationToken)
        {
            TakePending(seq)?.Completion.TrySetCanceled(cancellationToken);
        }

        private void FailAll(Exception ex)
        {
            Dictionary<uint, PendingRequest> abandoned;
            lock (_pendingGate)
            {
                if (_pending.Count == 0) return;
                abandoned = DetachPending();
            }
            FailPending(abandoned, ex);
        }

        private Dictionary<uint, PendingRequest> DetachPending()
        {
            var abandoned = _pending;
            _pending = new Dictionary<uint, PendingRequest>();
            return abandoned;
        }

        private static void FailPending(Dictionary<uint, PendingRequest> abandoned, Exception exception)
        {
            foreach (var pending in abandoned.Values)
                pending.Completion.TrySetException(exception);
        }

        private PendingRequest? TakePending(uint seq)
        {
            lock (_pendingGate)
                return _pending.Remove(seq, out var pending) ? pending : null;
        }

        private bool IsPending(uint seq, PendingRequest value)
        {
            lock (_pendingGate)
                return _pending.TryGetValue(seq, out var pending) && ReferenceEquals(pending, value);
        }

        private static ArraySegment<byte> Copy(ArraySegment<byte> src)
        {
            if (src.Array == null || src.Count <= 0) return default;
            /*
             * 异步响应可能在接收回调返回后才被消费，因此需要拥有副本，而不能返回传输缓冲区的视图。
             */
            var bytes = new byte[src.Count];
            Buffer.BlockCopy(src.Array, src.Offset, bytes, 0, src.Count);
            return new ArraySegment<byte>(bytes);
        }

        /// <summary>
        /// 停止接收新请求并令尚未取得完成权的请求失败。可重复调用，不关闭底层连接。
        /// </summary>
        public void Dispose()
        {
            Dictionary<uint, PendingRequest> abandoned;
            lock (_pendingGate)
            {
                if (_disposed) return;
                _disposed = true;
                abandoned = DetachPending();
            }

            FailPending(abandoned, new ObjectDisposedException(nameof(RequestClient)));
            _connection.PacketReceived -= OnPacketReceived;
            _connection.Disconnected -= OnDisconnected;
            _connection.Error -= OnError;
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
