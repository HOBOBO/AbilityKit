using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Host;
using AbilityKit.Network.Host.InProcess;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Ability.Host.Network
{
    /// <summary>
    /// 持有进程内监听器及其 Host 桥接。
    /// 请求仍经正式帧编解码、服务端会话和管线。
    /// </summary>
    public sealed class InProcessHostNetwork : IDisposable
    {
        private readonly object _gate = new object();
        private InProcessChannelListener _listener = null!;
        private bool _disposed;

        /// <summary>
        /// 创建使用同步处理器的进程内装配，不立即监听。
        /// </summary>
        public InProcessHostNetwork(
            IHostMessageCodec messageCodec,
            IHostNetworkRequestHandler requestHandler = null,
            IHostClientIdResolver clientIdResolver = null,
            NetworkHostOptions networkOptions = null)
        {
            Connections = new HostNetworkConnectionManager(
                CreateListener,
                messageCodec,
                requestHandler,
                clientIdResolver,
                networkOptions);
        }

        private InProcessHostNetwork(
            IHostMessageCodec messageCodec,
            IAsyncHostNetworkRequestHandler requestHandler,
            IHostClientIdResolver clientIdResolver,
            NetworkHostOptions networkOptions)
        {
            Connections = HostNetworkConnectionManager.CreateAsync(
                CreateListener,
                messageCodec,
                requestHandler,
                clientIdResolver,
                networkOptions);
        }

        /// <summary>
        /// 获取可装配到 HostRuntime 的连接管理器。
        /// </summary>
        public HostNetworkConnectionManager Connections { get; }

        /// <summary>
        /// 创建使用异步处理器的进程内装配，不立即监听。
        /// </summary>
        public static InProcessHostNetwork CreateAsync(
            IHostMessageCodec messageCodec,
            IAsyncHostNetworkRequestHandler requestHandler,
            IHostClientIdResolver clientIdResolver = null,
            NetworkHostOptions networkOptions = null)
        {
            return new InProcessHostNetwork(
                messageCodec,
                requestHandler ?? throw new ArgumentNullException(nameof(requestHandler)),
                clientIdResolver,
                networkOptions);
        }

        /// <summary>
        /// 创建由调用方释放的客户端连接；打开连接时要求本装配已监听。
        /// </summary>
        public ConnectionManager CreateClientConnection(
            ConnectionOptions options = null,
            IDispatcher callbackDispatcher = null,
            IDispatcher ioDispatcher = null)
        {
            ThrowIfDisposed();
            return new ConnectionManager(
                () => GetListeningListener().CreateClientTransport(),
                options,
                callbackDispatcher,
                ioDispatcher);
        }

        /// <summary>
        /// 创建并启动本轮进程内监听器。
        /// </summary>
        public void Start()
        {
            ThrowIfDisposed();
            Connections.Start();
        }

        /// <summary>
        /// 立即停止当前监听器并关闭会话。
        /// </summary>
        public void Stop()
        {
            if (_disposed) return;
            Connections.Stop();
        }

        /// <summary>
        /// 停止接入并按时限排空请求；等待原任务完成后才可重启。
        /// </summary>
        public Task StopAsync(
            TimeSpan drainTimeout,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return Connections.StopAsync(drainTimeout, cancellationToken);
        }

        /// <summary>
        /// 推进连接维护与超时检查。
        /// </summary>
        public void Tick()
        {
            ThrowIfDisposed();
            Connections.Tick();
        }

        /// <summary>Returns the current host diagnostics snapshot.</summary>
        public NetworkHostDiagnostics GetDiagnostics()
        {
            ThrowIfDisposed();
            return Connections.GetDiagnostics();
        }

        /// <summary>Returns transport-neutral snapshots for all active Sessions.</summary>
        public IReadOnlyList<NetworkHostSessionSnapshot> GetSessionSnapshots()
        {
            ThrowIfDisposed();
            return Connections.GetSessionSnapshots();
        }

        /// <summary>
        /// 释放拥有的连接管理器；停止排空期间应先等待异步任务。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            Connections.Dispose();
            lock (_gate) _listener = null!;
            _disposed = true;
        }

        private IChannelListener CreateListener()
        {
            var listener = new InProcessChannelListener();
            lock (_gate) _listener = listener;
            return listener;
        }

        private InProcessChannelListener GetListeningListener()
        {
            lock (_gate)
            {
                if (_listener == null || !_listener.IsListening)
                    throw new InvalidOperationException("Start the in-process host before opening a client connection.");
                return _listener;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(InProcessHostNetwork));
        }
    }
}
