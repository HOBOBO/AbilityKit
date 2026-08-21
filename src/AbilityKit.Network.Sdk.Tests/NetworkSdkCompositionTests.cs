using System.Threading;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Observability;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class NetworkSdkCompositionTests
{
    [Fact]
    public void ObserveTraffic_WithTransportFactory_InstallsConfiguredObserver()
    {
        var observer = new RecordingTrafficObserver();
        var transport = new RecordingTransport();
        using var client = new NetworkSdkBuilder()
            .UseTransportFactory(() => transport)
            .ObserveTraffic(observer, options =>
            {
                options.ConnectionId = "room-primary";
                options.Role = "room";
                options.CatalogId = "room";
                options.MaximumPayloadPreviewBytes = 1;
            })
            .Build();

        client.Open("gateway.example", 7100);
        client.SendPacket(77, new ArraySegment<byte>(new byte[] { 4, 5 }));

        var traffic = Assert.Single(observer.Events);
        Assert.Equal("room-primary", traffic.ConnectionId);
        Assert.Equal("room", traffic.Role);
        Assert.Equal("room", traffic.CatalogId);
        Assert.Equal(77u, traffic.OpCode);
        Assert.Equal(new byte[] { 4 }, traffic.PayloadPreview.ToArray());
    }

    [Fact]
    public void ObserveTraffic_WithExternalConnection_RejectsSilentInstallation()
    {
        var builder = new NetworkSdkBuilder()
            .UseConnectionFactory(() => new RecordingConnection())
            .ObserveTraffic(new RecordingTrafficObserver());

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("externally created connection", exception.Message);
    }

    [Fact]
    public async Task RequestClientFactory_CreatesOwnedRequestComponentPerSdkClient()
    {
        var connections = new List<RecordingConnection>();
        var requestClients = new List<RecordingRequestClient>();
        var builder = new NetworkSdkBuilder()
            .UseConnectionFactory(() =>
            {
                var connection = new RecordingConnection();
                connections.Add(connection);
                return connection;
            })
            .UseRequestClientFactory(connection =>
            {
                var requestClient = new RecordingRequestClient(connection);
                requestClients.Add(requestClient);
                return requestClient;
            });

        var first = builder.Build();
        var second = builder.Build();
        var response = await first.SendRawRequestAsync(901, new ArraySegment<byte>(new byte[] { 1, 2 }));

        Assert.Equal(2, connections.Count);
        Assert.Equal(2, requestClients.Count);
        Assert.Same(connections[0], requestClients[0].Connection);
        Assert.Same(connections[1], requestClients[1].Connection);
        Assert.Equal(901u, requestClients[0].LastOpCode);
        Assert.Equal(new byte[] { 9, 1 }, response.ToArray());

        first.Dispose();
        second.Dispose();

        Assert.All(requestClients, client => Assert.True(client.Disposed));
        Assert.All(connections, connection => Assert.Equal(1, connection.DisposeCount));
    }

    [Fact]
    public void RequestClientFactory_WhenReturningNull_DisposesOwnedConnection()
    {
        var connection = new RecordingConnection();
        var builder = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .UseRequestClientFactory(_ => null!);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal("Request client factory returned null.", exception.Message);
        Assert.Equal(1, connection.DisposeCount);
    }

    private sealed class RecordingRequestClient : IRequestClient
    {
        public RecordingRequestClient(IConnection connection)
        {
            Connection = connection;
        }

        public IConnection Connection { get; }
        public uint LastOpCode { get; private set; }
        public bool Disposed { get; private set; }

        public Task<ArraySegment<byte>> SendRequestAsync(
            uint opCode,
            ArraySegment<byte> payload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            LastOpCode = opCode;
            return Task.FromResult(new ArraySegment<byte>(new byte[] { 9, 1 }));
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class RecordingConnection : IConnection
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

    private sealed class RecordingTrafficObserver : INetworkTrafficObserver
    {
        public List<NetworkTrafficEvent> Events { get; } = new();

        public void OnTraffic(NetworkTrafficEvent trafficEvent) => Events.Add(trafficEvent);
    }

    private sealed class RecordingTransport : ITransport
    {
        public bool IsConnected { get; private set; }

        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<ArraySegment<byte>>? BytesReceived;

        public void Connect(string host, int port)
        {
            IsConnected = true;
            Connected?.Invoke();
        }

        public void Close() => IsConnected = false;
        public void Send(ArraySegment<byte> bytes) { }
        public void Dispose() => IsConnected = false;
    }
}
