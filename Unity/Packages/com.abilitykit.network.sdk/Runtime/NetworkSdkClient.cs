#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Network.Sdk
{
    /// <summary>
    /// Owns one connection and its single request client for the complete SDK lifetime.
    /// </summary>
    public sealed class NetworkSdkClient : IReconnectableConnection, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IReconnectableConnection? _reconnectableConnection;
        private readonly RequestClient _requestClient;
        private Action<uint, uint, ArraySegment<byte>>? _packetReceived;
        private bool _disposed;

        internal NetworkSdkClient(IConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _reconnectableConnection = connection as IReconnectableConnection;
            _requestClient = new RequestClient(_connection);

            _connection.Connected += HandleConnected;
            _connection.Disconnected += HandleDisconnected;
            _connection.Error += HandleError;
            _connection.ServerPushReceived += HandleServerPushReceived;
            _connection.Kicked += HandleKicked;

            if (_reconnectableConnection != null)
            {
                _reconnectableConnection.ReconnectScheduled += HandleReconnectScheduled;
                _reconnectableConnection.ReconnectAttemptStarted += HandleReconnectAttemptStarted;
                _reconnectableConnection.ReconnectExhausted += HandleReconnectExhausted;
            }
        }

        public ConnectionState State => _connection.State;

        public bool IsConnected => _connection.IsConnected;

        public bool SupportsReconnect => _reconnectableConnection != null;

        public bool IsReconnectExhausted =>
            _reconnectableConnection?.IsReconnectExhausted == true;

        public event Action? Connected;

        public event Action? Disconnected;

        public event Action<Exception>? Error;

        public event Action<uint, uint, ArraySegment<byte>>? PacketReceived
        {
            add
            {
                ThrowIfDisposed();
                if (_packetReceived == null)
                {
                    _connection.PacketReceived += HandlePacketReceived;
                }

                _packetReceived += value;
            }
            remove
            {
                if (_disposed)
                {
                    return;
                }

                _packetReceived -= value;
                if (_packetReceived == null)
                {
                    _connection.PacketReceived -= HandlePacketReceived;
                }
            }
        }

        public event Action<uint, ArraySegment<byte>>? ServerPushReceived;

        public event Action<string, string>? Kicked;

        public event Action<int, float>? ReconnectScheduled;

        public event Action<int>? ReconnectAttemptStarted;

        public event Action<int>? ReconnectExhausted;

        public void Open(string host, int port)
        {
            ThrowIfDisposed();
            _connection.Open(host, port);
        }

        /// <summary>
        /// Opens the connection only while it is disconnected.
        /// Returns false while connecting, connected, or reconnecting.
        /// </summary>
        public bool OpenIfDisconnected(string host, int port)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("Host is required.", nameof(host));
            }

            if (port <= 0 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            if (_connection.State != ConnectionState.Disconnected)
            {
                return false;
            }

            _connection.Open(host, port);
            return true;
        }

        public void Close()
        {
            if (_disposed)
            {
                return;
            }

            _connection.Close();
        }

        public void Tick(float deltaTime)
        {
            ThrowIfDisposed();
            _connection.Tick(deltaTime);
        }

        public void ResetReconnect()
        {
            ThrowIfDisposed();
            if (_reconnectableConnection == null)
            {
                throw new NotSupportedException(
                    "The configured network connection does not support reconnect control.");
            }

            _reconnectableConnection.ResetReconnect();
        }

        /// <summary>将连接与重连事件绑定到统一会话恢复信号接收器。</summary>
        public NetworkSdkClientRecoveryBinding BindRecoverySignals(
            INetworkSessionRecoverySignalSink signalSink,
            NetworkSdkClientRecoveryBindingOptions? options = null)
        {
            ThrowIfDisposed();
            return new NetworkSdkClientRecoveryBinding(this, signalSink, options);
        }

        public void SendPacket(
            uint opCode,
            ArraySegment<byte> payload,
            ushort flags = 0,
            uint seq = 0)
        {
            ThrowIfDisposed();
            _connection.Send(opCode, payload, flags, seq);
        }

        public Task<ArraySegment<byte>> SendRawRequestAsync(
            uint opCode,
            ArraySegment<byte> payload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return _requestClient.SendRequestAsync(opCode, payload, timeout, cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _connection.Connected -= HandleConnected;
            _connection.Disconnected -= HandleDisconnected;
            _connection.Error -= HandleError;
            if (_packetReceived != null)
            {
                _connection.PacketReceived -= HandlePacketReceived;
            }
            _connection.ServerPushReceived -= HandleServerPushReceived;
            _connection.Kicked -= HandleKicked;

            if (_reconnectableConnection != null)
            {
                _reconnectableConnection.ReconnectScheduled -= HandleReconnectScheduled;
                _reconnectableConnection.ReconnectAttemptStarted -= HandleReconnectAttemptStarted;
                _reconnectableConnection.ReconnectExhausted -= HandleReconnectExhausted;
            }

            _requestClient.Dispose();
            try
            {
                _connection.Close();
            }
            finally
            {
                _connection.Dispose();
            }

            Connected = null;
            Disconnected = null;
            Error = null;
            _packetReceived = null;
            ServerPushReceived = null;
            Kicked = null;
            ReconnectScheduled = null;
            ReconnectAttemptStarted = null;
            ReconnectExhausted = null;
        }

        private void HandleConnected()
        {
            Connected?.Invoke();
        }

        private void HandleDisconnected()
        {
            Disconnected?.Invoke();
        }

        private void HandleError(Exception exception)
        {
            Error?.Invoke(exception);
        }

        private void HandlePacketReceived(uint opCode, uint seq, ArraySegment<byte> payload)
        {
            _packetReceived?.Invoke(opCode, seq, payload);
        }

        private void HandleServerPushReceived(uint opCode, ArraySegment<byte> payload)
        {
            ServerPushReceived?.Invoke(opCode, payload);
        }

        private void HandleKicked(string code, string reason)
        {
            Kicked?.Invoke(code, reason);
        }

        private void HandleReconnectScheduled(int attemptNumber, float delaySeconds)
        {
            ReconnectScheduled?.Invoke(attemptNumber, delaySeconds);
        }

        private void HandleReconnectAttemptStarted(int attemptNumber)
        {
            ReconnectAttemptStarted?.Invoke(attemptNumber);
        }

        private void HandleReconnectExhausted(int attempts)
        {
            ReconnectExhausted?.Invoke(attempts);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NetworkSdkClient));
            }
        }
    }

    /// <summary>SDK 客户端连接事件到统一恢复信号的绑定选项。</summary>
    public sealed class NetworkSdkClientRecoveryBindingOptions
    {
        /// <summary>连接重新建立时是否上报连接恢复信号。</summary>
        public bool ReportConnectionRestored { get; set; } = true;

        /// <summary>是否将连接错误事件作为恢复信号上报。</summary>
        public bool ReportConnectionErrors { get; set; }

        /// <summary>不支持自动重连的连接断开时，是否直接按重连耗尽处理。</summary>
        public bool TreatDisconnectWithoutReconnectAsExhausted { get; set; } = true;

        /// <summary>提供信号关联帧；未设置时使用 0。</summary>
        public Func<int>? FrameProvider { get; set; }

        /// <summary>提供战斗、房间或连接的关联上下文。</summary>
        public Func<string?>? CorrelationContextProvider { get; set; }

        /// <summary>信号接收器或上下文提供器异常时的诊断回调。</summary>
        public Action<Exception>? ReportingFailure { get; set; }

        internal NetworkSdkClientRecoveryBindingOptions Snapshot()
        {
            return new NetworkSdkClientRecoveryBindingOptions
            {
                ReportConnectionRestored = ReportConnectionRestored,
                ReportConnectionErrors = ReportConnectionErrors,
                TreatDisconnectWithoutReconnectAsExhausted = TreatDisconnectWithoutReconnectAsExhausted,
                FrameProvider = FrameProvider,
                CorrelationContextProvider = CorrelationContextProvider,
                ReportingFailure = ReportingFailure
            };
        }
    }

    /// <summary>
    /// 负责订阅一个 <see cref="NetworkSdkClient"/> 的连接事件，并转换为统一恢复信号。
    /// 释放绑定不会释放 SDK 客户端。
    /// </summary>
    public sealed class NetworkSdkClientRecoveryBinding : IDisposable
    {
        private readonly NetworkSdkClient _client;
        private readonly INetworkSessionRecoverySignalSink _signalSink;
        private readonly NetworkSdkClientRecoveryBindingOptions _options;
        private int _enabled = 1;
        private int _recoveryInProgress;
        private int _disposed;

        internal NetworkSdkClientRecoveryBinding(
            NetworkSdkClient client,
            INetworkSessionRecoverySignalSink signalSink,
            NetworkSdkClientRecoveryBindingOptions? options)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _signalSink = signalSink ?? throw new ArgumentNullException(nameof(signalSink));
            _options = (options ?? new NetworkSdkClientRecoveryBindingOptions()).Snapshot();

            _client.Connected += HandleConnected;
            _client.Disconnected += HandleDisconnected;
            _client.ReconnectScheduled += HandleReconnectScheduled;
            _client.ReconnectAttemptStarted += HandleReconnectAttemptStarted;
            _client.ReconnectExhausted += HandleReconnectExhausted;
            if (_options.ReportConnectionErrors) _client.Error += HandleError;
        }

        /// <summary>获取或设置是否继续转换连接事件；关闭绑定时已有协调决策不会被清除。</summary>
        public bool Enabled
        {
            get => Volatile.Read(ref _enabled) != 0;
            set => Volatile.Write(ref _enabled, value ? 1 : 0);
        }

        /// <summary>
        /// 清除绑定内部记录的恢复中状态，但不修改协调器决策，也不改变当前启用状态。
        /// 主动关闭连接后再次启用绑定时可调用，避免下一次正常建连被误报为连接恢复。
        /// </summary>
        public void Reset()
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            Interlocked.Exchange(ref _recoveryInProgress, 0);
        }

        /// <summary>解除全部事件订阅。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Volatile.Write(ref _enabled, 0);
            _client.Connected -= HandleConnected;
            _client.Disconnected -= HandleDisconnected;
            _client.ReconnectScheduled -= HandleReconnectScheduled;
            _client.ReconnectAttemptStarted -= HandleReconnectAttemptStarted;
            _client.ReconnectExhausted -= HandleReconnectExhausted;
            if (_options.ReportConnectionErrors) _client.Error -= HandleError;
        }

        private void HandleConnected()
        {
            if (!Enabled || !_options.ReportConnectionRestored ||
                Interlocked.Exchange(ref _recoveryInProgress, 0) == 0)
            {
                return;
            }

            Report(
                NetworkSessionRecoverySignalKind.ConnectionRestored,
                SyncHealthSeverity.Info,
                exception: null,
                "连接已经重新建立。");
        }

        private void HandleDisconnected()
        {
            if (!Enabled) return;
            Interlocked.Exchange(ref _recoveryInProgress, 1);
            var exhausted = !_client.SupportsReconnect &&
                _options.TreatDisconnectWithoutReconnectAsExhausted;
            Report(
                exhausted
                    ? NetworkSessionRecoverySignalKind.ReconnectExhausted
                    : NetworkSessionRecoverySignalKind.ConnectionLost,
                exhausted ? SyncHealthSeverity.Error : SyncHealthSeverity.Warning,
                exception: null,
                exhausted
                    ? "连接已断开且当前连接不支持自动重连。"
                    : "连接已断开，等待连接层安排重连。");
        }

        private void HandleReconnectScheduled(int attemptNumber, float delaySeconds)
        {
            if (!Enabled) return;
            Interlocked.Exchange(ref _recoveryInProgress, 1);
            Report(
                NetworkSessionRecoverySignalKind.ReconnectScheduled,
                SyncHealthSeverity.Warning,
                exception: null,
                $"已安排第 {attemptNumber} 次重连，延迟 {delaySeconds} 秒。");
        }

        private void HandleReconnectAttemptStarted(int attemptNumber)
        {
            if (!Enabled) return;
            Interlocked.Exchange(ref _recoveryInProgress, 1);
            Report(
                NetworkSessionRecoverySignalKind.ReconnectAttemptStarted,
                SyncHealthSeverity.Warning,
                exception: null,
                $"正在执行第 {attemptNumber} 次重连。");
        }

        private void HandleReconnectExhausted(int attempts)
        {
            if (!Enabled) return;
            Interlocked.Exchange(ref _recoveryInProgress, 1);
            Report(
                NetworkSessionRecoverySignalKind.ReconnectExhausted,
                SyncHealthSeverity.Error,
                exception: null,
                $"自动重连已在 {attempts} 次尝试后耗尽。");
        }

        private void HandleError(Exception exception)
        {
            if (!Enabled) return;
            Report(
                NetworkSessionRecoverySignalKind.ConnectionError,
                SyncHealthSeverity.Error,
                exception,
                "连接层报告异常。");
        }

        private void Report(
            NetworkSessionRecoverySignalKind kind,
            SyncHealthSeverity severity,
            Exception? exception,
            string detail)
        {
            try
            {
                var frame = _options.FrameProvider?.Invoke() ?? 0;
                var correlationContext = _options.CorrelationContextProvider?.Invoke();
                var signal = new NetworkSessionRecoverySignal(
                    kind,
                    severity,
                    frame,
                    exception,
                    correlationContext,
                    detail);
                _signalSink.TryReport(in signal, out _);
            }
            catch (Exception reportingFailure)
            {
                try { _options.ReportingFailure?.Invoke(reportingFailure); } catch { }
            }
        }
    }
}
