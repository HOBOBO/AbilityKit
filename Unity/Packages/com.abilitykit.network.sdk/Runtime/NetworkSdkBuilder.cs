#nullable enable

using System;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Observability;

namespace AbilityKit.Network.Sdk
{
    /// <summary>Creates the request/response component owned by an SDK client.</summary>
    public delegate IRequestClient NetworkRequestClientFactory(IConnection connection);

    /// <summary>
    /// Composes the transport-independent network SDK client.
    /// </summary>
    public sealed class NetworkSdkBuilder
    {
        private Func<IConnection>? _connectionFactory;
        private Func<ITransport>? _transportFactory;
        private Action<ConnectionOptions>? _configureConnection;
        private NetworkRequestClientFactory? _requestClientFactory;
        private NetworkTrafficObserverFactory? _trafficObserverFactory;
        private Action<NetworkTrafficCaptureOptions>? _configureTrafficCapture;
        private IDispatcher? _callbackDispatcher;
        private IDispatcher? _ioDispatcher;

        public NetworkSdkBuilder UseConnectionFactory(Func<IConnection> connectionFactory)
        {
            return UseOwnedConnectionFactory(connectionFactory);
        }

        /// <summary>
        /// Configures a connection factory whose returned connection is owned and disposed by the SDK client.
        /// </summary>
        public NetworkSdkBuilder UseOwnedConnectionFactory(Func<IConnection> connectionFactory)
        {
            _connectionFactory = connectionFactory
                ?? throw new ArgumentNullException(nameof(connectionFactory));
            _transportFactory = null;
            return this;
        }

        public NetworkSdkBuilder UseTransportFactory(Func<ITransport> transportFactory)
        {
            _transportFactory = transportFactory
                ?? throw new ArgumentNullException(nameof(transportFactory));
            _connectionFactory = null;
            return this;
        }

        public NetworkSdkBuilder ConfigureConnection(Action<ConnectionOptions> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            _configureConnection += configure;
            return this;
        }

        /// <summary>
        /// Observes traffic from connections created through <see cref="UseTransportFactory"/>.
        /// The observer may be shared by multiple SDK clients; event connection metadata keeps
        /// their logical connections and physical reconnect generations distinct.
        /// </summary>
        public NetworkSdkBuilder ObserveTraffic(
            INetworkTrafficObserver observer,
            Action<NetworkTrafficCaptureOptions>? configure = null)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            return ConfigureTrafficCapture(_ => observer, configure);
        }

        /// <summary>
        /// Configures a per-physical-session observer factory. This is the extension point for
        /// connection-scoped exporters, decoders, tracing bridges, and visualization streams.
        /// </summary>
        public NetworkSdkBuilder ConfigureTrafficCapture(
            NetworkTrafficObserverFactory observerFactory,
            Action<NetworkTrafficCaptureOptions>? configure = null)
        {
            _trafficObserverFactory = observerFactory
                ?? throw new ArgumentNullException(nameof(observerFactory));
            _configureTrafficCapture = configure;
            return this;
        }

        /// <summary>
        /// Configures the request/response implementation. Each built SDK client invokes the
        /// factory once and owns the returned request client for its complete lifetime.
        /// </summary>
        public NetworkSdkBuilder UseRequestClientFactory(NetworkRequestClientFactory factory)
        {
            _requestClientFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        public NetworkSdkBuilder UseDispatchers(
            IDispatcher callbackDispatcher,
            IDispatcher? ioDispatcher = null)
        {
            _callbackDispatcher = callbackDispatcher
                ?? throw new ArgumentNullException(nameof(callbackDispatcher));
            _ioDispatcher = ioDispatcher ?? callbackDispatcher;
            return this;
        }

        public NetworkSdkClient Build()
        {
            var connection = CreateConnection();
            if (connection == null)
            {
                throw new InvalidOperationException("Network connection factory returned null.");
            }

            try
            {
                return new NetworkSdkClient(connection, _requestClientFactory);
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private IConnection CreateConnection()
        {
            if (_connectionFactory != null)
            {
                if (_trafficObserverFactory != null)
                {
                    throw new InvalidOperationException(
                        "Traffic capture cannot be installed on an externally created connection. " +
                        "Use a transport factory or install a probe in that connection's pipeline.");
                }

                return _connectionFactory.Invoke();
            }

            if (_transportFactory == null)
            {
                throw new InvalidOperationException(
                    "Configure a connection factory or transport factory before building the network SDK client.");
            }

            var options = new ConnectionOptions();
            _configureConnection?.Invoke(options);
            if (_trafficObserverFactory != null)
            {
                var capture = new NetworkTrafficCaptureOptions();
                _configureTrafficCapture?.Invoke(capture);
                capture.ObserverFactory = _trafficObserverFactory;
                options.TrafficCapture = capture;
            }
            return new ConnectionManager(
                _transportFactory,
                options,
                _callbackDispatcher ?? InlineDispatcher.Instance,
                _ioDispatcher ?? InlineDispatcher.Instance);
        }
    }
}
