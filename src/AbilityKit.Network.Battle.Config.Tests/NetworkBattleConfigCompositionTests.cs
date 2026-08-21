using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Battle.Config;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Observability;
using AbilityKit.Network.Sdk;
using Xunit;

namespace AbilityKit.Network.Battle.Config.Tests;

public sealed class NetworkBattleConfigCompositionTests
{
    [Fact]
    public void WithSdkClient_BuildsWithoutTransportOrConnectionFactory()
    {
        var connection = new RecordingConnection();
        using var sdkClient = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();

        var options = CreateConfiguredBuilder()
            .WithSdkClient(sdkClient, NetworkSdkClientOwnership.Owned)
            .Build();

        Assert.Same(sdkClient, options.SdkClient);
        Assert.Equal(NetworkSdkClientOwnership.Owned, options.SdkClientOwnership);
        Assert.Null(options.SdkClientFactory);
        Assert.Null(options.ConnectionFactory);
        Assert.Null(options.TransportFactory);
    }

    [Fact]
    public void WithSdkClientFactory_LastSourceWinsAndFactoryIsPreserved()
    {
        var connection = new RecordingConnection();
        using var sdkClient = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();
        Func<NetworkSdkClient> factory = () => sdkClient;

        var options = CreateConfiguredBuilder()
            .WithTransportFactory(() => throw new NotSupportedException())
            .WithSdkClientFactory(factory)
            .Build();

        Assert.Same(factory, options.SdkClientFactory);
        Assert.Null(options.SdkClient);
        Assert.Null(options.ConnectionFactory);
        Assert.Null(options.TransportFactory);
    }

    [Fact]
    public void ConnectionConfiguration_CanBeReplacedOrDisabled()
    {
        Action<ConnectionOptions> configure = options => options.EnableReconnect = false;
        var configured = CreateConfiguredBuilder()
            .WithTransportFactory(() => throw new NotSupportedException())
            .WithConnectionConfiguration(configure)
            .Build();
        var sdkDefaults = CreateConfiguredBuilder()
            .WithTransportFactory(() => throw new NotSupportedException())
            .UseSdkConnectionDefaults()
            .Build();

        Assert.Same(configure, configured.ConfigureConnection);
        Assert.Null(sdkDefaults.ConfigureConnection);
    }

    [Fact]
    public void ObserveTraffic_PreservesObserverAndCapturePolicy()
    {
        var observer = new RecordingObserver();
        Action<NetworkTrafficCaptureOptions> configure = options =>
        {
            options.Role = "battle";
            options.CatalogId = "project.battle";
        };

        var options = CreateConfiguredBuilder()
            .WithTcpTransport()
            .ObserveTraffic(observer, configure)
            .Build();

        Assert.Same(observer, options.TrafficObserver);
        Assert.Same(configure, options.ConfigureTrafficCapture);
    }

    private static NetworkBattleConfig CreateConfiguredBuilder()
    {
        return new NetworkBattleConfig()
            .WithGateway("gateway.example", 7100)
            .WithSession("token", "battle")
            .UseRoomGatewayProtocol("battle")
            .WithInputSerializer(
                _ => default,
                _ => new NetworkSubmitInputResponse());
    }

    private sealed class RecordingConnection : IConnection
    {
        public ConnectionState State { get; private set; }
        public bool IsConnected => State == ConnectionState.Connected;

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
        public void Dispose() => State = ConnectionState.Disconnected;
    }

    private sealed class RecordingObserver : INetworkTrafficObserver
    {
        public void OnTraffic(NetworkTrafficEvent trafficEvent) { }
    }
}
