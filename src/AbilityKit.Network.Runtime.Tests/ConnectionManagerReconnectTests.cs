using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class ConnectionManagerReconnectTests
{
    [Fact]
    public void ConnectionOptions_DoNotEnableBusinessKickProtocolByDefault()
    {
        var options = new ConnectionOptions();

        Assert.False(options.EnableKickHandling);
        Assert.Equal(0U, options.KickPushOpCode);
    }

    [Fact]
    public void ReconnectCadence_PreservesAttemptsAcrossTransportReplacementAndStopsAtLimit()
    {
        var transports = new List<TestTransport>();
        using var connection = CreateConnection(transports, maxAttempts: 2);

        connection.Open("127.0.0.1", 4000);
        Assert.Single(transports);

        transports[0].Fail();
        Assert.Equal(ConnectionState.Reconnecting, connection.State);
        connection.Tick(0.99f);
        Assert.Single(transports);
        connection.Tick(0.01f);
        Assert.Equal(2, transports.Count);

        transports[1].Fail();
        connection.Tick(1.99f);
        Assert.Equal(2, transports.Count);
        connection.Tick(0.01f);
        Assert.Equal(3, transports.Count);

        transports[2].Fail();
        Assert.Equal(ConnectionState.Disconnected, connection.State);
        Assert.True(connection.IsReconnectExhausted);
        connection.Tick(100f);
        Assert.Equal(3, transports.Count);

        connection.Open("127.0.0.1", 4000);
        Assert.Equal(3, transports.Count);
        connection.ResetReconnect();
        Assert.Equal(4, transports.Count);
        Assert.False(connection.IsReconnectExhausted);
    }

    [Fact]
    public void SuccessfulReconnect_ResetsAttemptBudgetForLaterDisconnect()
    {
        var transports = new List<TestTransport>();
        using var connection = CreateConnection(transports, maxAttempts: 1);

        connection.Open("127.0.0.1", 4000);
        transports[0].Fail();
        connection.Tick(1f);
        transports[1].Succeed();
        Assert.Equal(ConnectionState.Connected, connection.State);

        transports[1].Fail();
        Assert.Equal(ConnectionState.Reconnecting, connection.State);
        connection.Tick(1f);

        Assert.Equal(3, transports.Count);
    }

    private static ConnectionManager CreateConnection(
        List<TestTransport> transports,
        int maxAttempts)
    {
        return new ConnectionManager(
            () =>
            {
                var transport = new TestTransport();
                transports.Add(transport);
                return transport;
            },
            new ConnectionOptions
            {
                FrameCodec = LengthPrefixedFrameCodec.Instance,
                EnableReconnect = true,
                ReconnectInitialDelay = TimeSpan.FromSeconds(1),
                ReconnectMaxDelay = TimeSpan.FromSeconds(2),
                ReconnectBackoffMultiplier = 2d,
                ReconnectMaxAttempts = maxAttempts
            });
    }

    private sealed class TestTransport : ITransport
    {
        public bool IsConnected { get; private set; }

        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<ArraySegment<byte>>? BytesReceived;

        public void Connect(string host, int port)
        {
        }

        public void Close()
        {
            IsConnected = false;
        }

        public void Send(ArraySegment<byte> bytes)
        {
        }

        public void Succeed()
        {
            IsConnected = true;
            Connected?.Invoke();
        }

        public void Fail()
        {
            IsConnected = false;
            Error?.Invoke(new InvalidOperationException("simulated transport failure"));
        }

        public void Dispose()
        {
            IsConnected = false;
        }
    }
}
