#nullable enable

using System;
using System.Threading;
using AbilityKit.GameFramework.Network;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using GameFramework.Network;

namespace AbilityKit.Demo.Shooter.View
{
    public interface IShooterClientConnectionFactory
    {
        IConnection CreateConnection();
    }

    public sealed class ShooterClientConnectionFactory : IShooterClientConnectionFactory
    {
        private readonly Func<IConnection> _connectionFactory;

        public ShooterClientConnectionFactory(Func<IConnection> connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        }

        public IConnection CreateConnection()
        {
            var connection = _connectionFactory();
            if (connection == null)
            {
                throw new InvalidOperationException("Shooter client connection factory returned null.");
            }

            return connection;
        }

        public static ShooterClientConnectionFactory FromTransportFactory(Func<ITransport> transportFactory, ConnectionOptions? options = null, IDispatcher? callbackDispatcher = null, IDispatcher? ioDispatcher = null)
        {
            if (transportFactory == null)
            {
                throw new ArgumentNullException(nameof(transportFactory));
            }

            return new ShooterClientConnectionFactory(() =>
                new ConnectionManager(
                    transportFactory,
                    options ?? CreateDefaultOptions(),
                    callbackDispatcher ?? InlineDispatcher.Instance,
                    ioDispatcher ?? InlineDispatcher.Instance));
        }

        public static ShooterClientConnectionFactory Tcp(ConnectionOptions? options = null, IDispatcher? callbackDispatcher = null, IDispatcher? ioDispatcher = null)
        {
            return FromTransportFactory(() => new TcpTransport(), options, callbackDispatcher, ioDispatcher);
        }

        public static ShooterClientConnectionFactory TcpForUnityMainThread(
            ConnectionOptions? options = null,
            IDispatcher? ioDispatcher = null)
        {
            var synchronizationContext = SynchronizationContext.Current
                ?? throw new InvalidOperationException(
                    "SynchronizationContext.Current is null. Capture must be called on the Unity main thread.");
            return Tcp(options, new SynchronizationContextDispatcher(synchronizationContext), ioDispatcher);
        }

        public static ShooterClientConnectionFactory FromGameFrameworkNetwork(INetworkManager networkManager, string channelName = "ShooterGateway", ServiceType serviceType = ServiceType.Tcp)
        {
            if (networkManager == null)
            {
                throw new ArgumentNullException(nameof(networkManager));
            }

            return new ShooterClientConnectionFactory(() => GameFrameworkGatewayConnectionFactory.Create(networkManager, channelName, serviceType));
        }

        public static ShooterClientConnectionFactory FromGameFrameworkChannel(INetworkChannel channel)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            return new ShooterClientConnectionFactory(() => GameFrameworkGatewayConnectionFactory.Wrap(channel));
        }

        public static ConnectionOptions CreateDefaultOptions()
        {
            return new ConnectionOptions();
        }

        internal sealed class SynchronizationContextDispatcher : IDispatcher
        {
            private readonly SynchronizationContext _synchronizationContext;

            public SynchronizationContextDispatcher(SynchronizationContext synchronizationContext)
            {
                _synchronizationContext = synchronizationContext;
            }

            public void Post(Action action)
            {
                if (action == null) throw new ArgumentNullException(nameof(action));
                _synchronizationContext.Post(_ => action(), null);
            }
        }
    }
}
