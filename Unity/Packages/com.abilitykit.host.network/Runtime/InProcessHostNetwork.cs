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
    /// Owns the bundled in-process listener and its Host adapter. This is a composition
    /// convenience; requests still pass through the normal framing, session, and pipeline stack.
    /// </summary>
    public sealed class InProcessHostNetwork : IDisposable
    {
        private readonly object _gate = new object();
        private InProcessChannelListener _listener = null!;
        private bool _disposed;

        /// <summary>Creates a synchronous-handler in-process Host network composition.</summary>
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

        /// <summary>Gets the connection manager to attach to a HostRuntime or WorldHostBuilder.</summary>
        public HostNetworkConnectionManager Connections { get; }

        /// <summary>Creates an asynchronous-handler in-process Host network composition.</summary>
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

        /// <summary>Creates a client connection backed by the listener owned by this composition.</summary>
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

        /// <summary>Creates and starts a fresh in-process listener.</summary>
        public void Start()
        {
            ThrowIfDisposed();
            Connections.Start();
        }

        /// <summary>Stops the current listener and disconnects its sessions.</summary>
        public void Stop()
        {
            if (_disposed) return;
            Connections.Stop();
        }

        /// <summary>Stops accepting requests, drains queued handlers, then disconnects clients.</summary>
        public Task StopAsync(
            TimeSpan drainTimeout,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return Connections.StopAsync(drainTimeout, cancellationToken);
        }

        /// <summary>Runs connection maintenance such as idle timeout checks.</summary>
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

        /// <summary>Stops and releases the owned connection manager and listener.</summary>
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
