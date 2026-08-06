using AbilityKit.Network.Battle;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
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
}
