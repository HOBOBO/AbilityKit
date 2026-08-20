using AbilityKit.Network.Battle;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using Xunit;

namespace AbilityKit.Network.Battle.Tests;

public sealed class NetworkTransportOwnershipTests
{
    [Fact]
    public void GenericNetworkClient_PublicConstructor_OwnsOneSubscriptionChainAndDisposesOnce()
    {
        var transport = new ObservableTransport();
        var client = new GenericNetworkClient(
            () => transport,
            LengthPrefixedFrameCodec.Instance);

        client.Connect("gateway.example", 17001);

        AssertSingleSubscriptionChain(transport);
        Assert.Equal("gateway.example", transport.Host);
        Assert.Equal(17001, transport.Port);

        client.Dispose();
        client.Dispose();

        AssertReleasedOnce(transport);
    }

    [Fact]
    public void NetworkTransport_PublicConstructor_OwnsOneSubscriptionChainAndDisposesOnce()
    {
        var transport = new ObservableTransport();
        var options = new NetworkTransportOptions
        {
            Host = "battle.example",
            Port = 17002,
            TransportFactory = () => transport,
            FrameCodec = LengthPrefixedFrameCodec.Instance
        };
        var client = new NetworkTransport(options);

        client.Connect();

        AssertSingleSubscriptionChain(transport);
        Assert.Equal("battle.example", transport.Host);
        Assert.Equal(17002, transport.Port);

        client.Dispose();
        client.Dispose();

        AssertReleasedOnce(transport);
    }

    [Fact]
    public void NetworkTransport_PreconstructedBorrowedSdkClient_RemainsAliveAfterDispose()
    {
        var connection = new ObservableConnection();
        var sdkClient = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();
        var transport = new NetworkTransport(new NetworkTransportOptions
        {
            SdkClient = sdkClient
        });

        transport.Dispose();

        Assert.Equal(0, connection.DisposeCount);

        sdkClient.Dispose();
        Assert.Equal(1, connection.DisposeCount);
    }

    [Fact]
    public void NetworkTransport_PreconstructedOwnedSdkClient_IsDisposedOnce()
    {
        var connection = new ObservableConnection();
        var sdkClient = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();
        var transport = new NetworkTransport(new NetworkTransportOptions
        {
            SdkClient = sdkClient,
            SdkClientOwnership = NetworkSdkClientOwnership.Owned
        });

        transport.Dispose();
        transport.Dispose();
        sdkClient.Dispose();

        Assert.Equal(1, connection.DisposeCount);
    }

    [Fact]
    public void NetworkTransport_SdkClientFactory_ReturnedClientIsOwned()
    {
        var connection = new ObservableConnection();
        var sdkClient = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();
        var factoryCalls = 0;
        var transport = new NetworkTransport(new NetworkTransportOptions
        {
            SdkClientFactory = () =>
            {
                factoryCalls++;
                return sdkClient;
            }
        });

        transport.Dispose();

        Assert.Equal(1, factoryCalls);
        Assert.Equal(1, connection.DisposeCount);
    }

    [Fact]
    public void NetworkTransport_CompleteConnectionConfiguration_ReachesRuntimeFactories()
    {
        var transport = new ObservableTransport();
        var schedulerMaxAttempts = 0;
        var heartbeatFactoryCalls = 0;
        IFrameCodec? configuredCodec = null;
        var options = new NetworkTransportOptions
        {
            Host = "battle.example",
            Port = 17003,
            TransportFactory = () => transport,
            FrameCodec = LengthPrefixedFrameCodec.Instance,
            ConfigureConnection = connectionOptions =>
            {
                configuredCodec = connectionOptions.FrameCodec;
                connectionOptions.ReconnectMaxAttempts = 4;
                connectionOptions.ReconnectSchedulerFactory = context =>
                {
                    schedulerMaxAttempts = context.MaxAttempts;
                    return new ReconnectAttemptScheduler(context.MaxAttempts, context.ResolveDelay);
                };
                connectionOptions.HeartbeatFactory = opCode =>
                {
                    heartbeatFactoryCalls++;
                    return new HeartbeatMiddleware(opCode);
                };
            }
        };

        using var client = new NetworkTransport(options);
        client.Connect();

        Assert.Same(LengthPrefixedFrameCodec.Instance, configuredCodec);
        Assert.Equal(4, schedulerMaxAttempts);
        Assert.Equal(1, heartbeatFactoryCalls);
    }

    [Fact]
    public void NetworkTransportOptions_DefaultConnectionConfiguration_PreservesBattleCadence()
    {
        var options = new NetworkTransportOptions();
        var connectionOptions = new ConnectionOptions();

        options.ConfigureConnection(connectionOptions);

        Assert.True(connectionOptions.EnableReconnect);
        Assert.Equal(TimeSpan.FromSeconds(ReconnectBackoffPolicy.BaseDelaySeconds), connectionOptions.ReconnectInitialDelay);
        Assert.Equal(TimeSpan.FromSeconds(ReconnectBackoffPolicy.MaxDelaySeconds), connectionOptions.ReconnectMaxDelay);
        Assert.Equal(2d, connectionOptions.ReconnectBackoffMultiplier);
        Assert.Equal(ReconnectBackoffPolicy.MaxAttempts, connectionOptions.ReconnectMaxAttempts);
    }

    [Fact]
    public void NetworkTransport_MultipleSdkClientSources_AreRejected()
    {
        var connection = new ObservableConnection();
        using var sdkClient = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();

        var exception = Assert.Throws<ArgumentException>(() =>
            new NetworkTransport(new NetworkTransportOptions
            {
                SdkClient = sdkClient,
                SdkClientFactory = () => sdkClient
            }));

        Assert.Contains("either SdkClient or SdkClientFactory", exception.Message);
    }

    private static void AssertSingleSubscriptionChain(ObservableTransport transport)
    {
        Assert.Equal(1, transport.ConnectedSubscriberCount);
        Assert.Equal(1, transport.DisconnectedSubscriberCount);
        Assert.Equal(1, transport.ErrorSubscriberCount);
        Assert.Equal(2, transport.BytesReceivedSubscriberCount);
    }

    private static void AssertReleasedOnce(ObservableTransport transport)
    {
        Assert.Equal(1, transport.DisposeCount);
        Assert.Equal(0, transport.ConnectedSubscriberCount);
        Assert.Equal(0, transport.DisconnectedSubscriberCount);
        Assert.Equal(0, transport.ErrorSubscriberCount);
        Assert.Equal(0, transport.BytesReceivedSubscriberCount);
    }

    private sealed class ObservableTransport : ITransport
    {
        private Action? _connected;
        private Action? _disconnected;
        private Action<Exception>? _error;
        private Action<ArraySegment<byte>>? _bytesReceived;

        public bool IsConnected { get; private set; }
        public string? Host { get; private set; }
        public int Port { get; private set; }
        public int DisposeCount { get; private set; }
        public int ConnectedSubscriberCount => _connected?.GetInvocationList().Length ?? 0;
        public int DisconnectedSubscriberCount => _disconnected?.GetInvocationList().Length ?? 0;
        public int ErrorSubscriberCount => _error?.GetInvocationList().Length ?? 0;
        public int BytesReceivedSubscriberCount => _bytesReceived?.GetInvocationList().Length ?? 0;

        public event Action Connected
        {
            add => _connected += value;
            remove => _connected -= value;
        }

        public event Action Disconnected
        {
            add => _disconnected += value;
            remove => _disconnected -= value;
        }

        public event Action<Exception> Error
        {
            add => _error += value;
            remove => _error -= value;
        }

        public event Action<ArraySegment<byte>> BytesReceived
        {
            add => _bytesReceived += value;
            remove => _bytesReceived -= value;
        }

        public void Connect(string host, int port)
        {
            Host = host;
            Port = port;
            IsConnected = true;
        }

        public void Close()
        {
            IsConnected = false;
        }

        public void Send(ArraySegment<byte> bytes)
        {
        }

        public void Dispose()
        {
            DisposeCount++;
            IsConnected = false;
        }
    }

    private sealed class ObservableConnection : IConnection
    {
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public bool IsConnected => State == ConnectionState.Connected;
        public int DisposeCount { get; private set; }

        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<uint, uint, ArraySegment<byte>>? PacketReceived;
        public event Action<uint, ArraySegment<byte>>? ServerPushReceived;
        public event Action<string, string>? Kicked;

        public void Open(string host, int port) => State = ConnectionState.Connected;
        public void Close() => State = ConnectionState.Disconnected;
        public void Tick(float deltaTime) { }
        public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0) { }

        public void Dispose()
        {
            DisposeCount++;
            State = ConnectionState.Disconnected;
        }
    }
}
