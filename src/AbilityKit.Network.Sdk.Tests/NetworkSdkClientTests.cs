using System.Buffers.Binary;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.TcpGateway;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class NetworkSdkClientTests
{
    [Fact]
    public void Build_WithoutFactory_Throws()
    {
        var builder = new NetworkSdkBuilder();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("connection factory or transport factory", exception.Message);
    }

    [Fact]
    public void Build_WhenConnectionFactoryReturnsNull_Throws()
    {
        var builder = new NetworkSdkBuilder()
            .UseConnectionFactory(() => null!);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal("Network connection factory returned null.", exception.Message);
    }

    [Fact]
    public void Build_WhenClientConstructionFails_DisposesConnection()
    {
        var connection = new ThrowingSubscriptionConnection();
        var builder = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal("subscription failed", exception.Message);
        Assert.Equal(1, connection.DisposeCount);
    }

    [Fact]
    public void Build_CreatesIndependentClientsAndConnections()
    {
        var connections = new List<ObservableConnection>();
        var builder = new NetworkSdkBuilder()
            .UseConnectionFactory(() =>
            {
                var connection = new ObservableConnection();
                connections.Add(connection);
                return connection;
            });

        using var first = builder.Build();
        using var second = builder.Build();

        Assert.Equal(2, connections.Count);
        Assert.NotSame(connections[0], connections[1]);
    }

    [Fact]
    public void TransportFactory_CreatesIndependentOptionsAndTransportsPerBuild()
    {
        var configureCount = 0;
        var transports = new List<ObservableTransport>();
        var builder = new NetworkSdkBuilder()
            .UseTransportFactory(() =>
            {
                var transport = new ObservableTransport();
                transports.Add(transport);
                return transport;
            })
            .ConfigureConnection(options =>
            {
                configureCount++;
                options.EnableReconnect = false;
            });

        using var first = builder.Build();
        using var second = builder.Build();

        Assert.Equal(2, configureCount);
        Assert.Empty(transports);

        first.Open("first.example", 7101);
        second.Open("second.example", 7102);

        Assert.Equal(2, transports.Count);
        Assert.Equal("first.example", transports[0].ConnectHost);
        Assert.Equal(7101, transports[0].ConnectPort);
        Assert.Equal("second.example", transports[1].ConnectHost);
        Assert.Equal(7102, transports[1].ConnectPort);
    }

    [Fact]
    public void Build_CreatesOneRequestSubscriptionChain()
    {
        var connection = new ObservableConnection();

        using var client = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();

        Assert.Equal(1, connection.PacketReceivedSubscriberCount);
        Assert.Equal(2, connection.DisconnectedSubscriberCount);
        Assert.Equal(2, connection.ErrorSubscriberCount);
        Assert.Equal(1, connection.ConnectedSubscriberCount);
        Assert.Equal(1, connection.ServerPushSubscriberCount);
        Assert.Equal(1, connection.KickedSubscriberCount);
    }

    [Fact]
    public void OpenCloseAndTick_DelegateToConnection()
    {
        var connection = new ObservableConnection();
        using var client = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();

        client.Open("gateway.example", 7100);
        client.Tick(0.25f);
        client.Close();

        Assert.Equal("gateway.example", connection.OpenHost);
        Assert.Equal(7100, connection.OpenPort);
        Assert.Equal(0.25f, connection.LastTickDelta);
        Assert.Equal(1, connection.OpenCount);
        Assert.Equal(1, connection.CloseCount);
    }

    [Fact]
    public void ConnectionEvents_AreForwardedWithoutTranslation()
    {
        var connection = new ObservableConnection();
        using var client = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();
        var connected = 0;
        var disconnected = 0;
        Exception? actualError = null;
        uint actualPushOpCode = 0;
        byte[]? actualPushPayload = null;
        string? actualKickCode = null;
        string? actualKickReason = null;

        client.Connected += () => connected++;
        client.Disconnected += () => disconnected++;
        client.Error += exception => actualError = exception;
        client.ServerPushReceived += (opCode, payload) =>
        {
            actualPushOpCode = opCode;
            actualPushPayload = payload.ToArray();
        };
        client.Kicked += (code, reason) =>
        {
            actualKickCode = code;
            actualKickReason = reason;
        };

        var expectedError = new IOException("transport failed");
        connection.RaiseConnected();
        connection.RaiseDisconnected();
        connection.RaiseError(expectedError);
        connection.RaiseServerPush(901, Bytes(4, 5, 6));
        connection.RaiseKicked("session-replaced", "signed in elsewhere");

        Assert.Equal(1, connected);
        Assert.Equal(1, disconnected);
        Assert.Same(expectedError, actualError);
        Assert.Equal(901u, actualPushOpCode);
        Assert.Equal(new byte[] { 4, 5, 6 }, actualPushPayload);
        Assert.Equal("session-replaced", actualKickCode);
        Assert.Equal("signed in elsewhere", actualKickReason);
    }

    [Fact]
    public void ReconnectCapability_WhenSupported_ForwardsStateEventsAndReset()
    {
        var connection = new ReconnectableObservableConnection();
        var client = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();
        var scheduled = default((int Attempt, float Delay));
        var startedAttempt = 0;
        var exhaustedAttempts = 0;

        client.ReconnectScheduled += (attempt, delay) => scheduled = (attempt, delay);
        client.ReconnectAttemptStarted += attempt => startedAttempt = attempt;
        client.ReconnectExhausted += attempts => exhaustedAttempts = attempts;

        connection.IsReconnectExhausted = true;
        connection.RaiseReconnectScheduled(2, 1.5f);
        connection.RaiseReconnectAttemptStarted(2);
        connection.RaiseReconnectExhausted(4);
        client.ResetReconnect();

        Assert.True(client.SupportsReconnect);
        Assert.False(client.IsReconnectExhausted);
        Assert.Equal((2, 1.5f), scheduled);
        Assert.Equal(2, startedAttempt);
        Assert.Equal(4, exhaustedAttempts);
        Assert.Equal(1, connection.ResetReconnectCount);
        Assert.Equal(1, connection.ReconnectScheduledSubscriberCount);
        Assert.Equal(1, connection.ReconnectAttemptStartedSubscriberCount);
        Assert.Equal(1, connection.ReconnectExhaustedSubscriberCount);

        client.Dispose();

        Assert.Equal(0, connection.ReconnectScheduledSubscriberCount);
        Assert.Equal(0, connection.ReconnectAttemptStartedSubscriberCount);
        Assert.Equal(0, connection.ReconnectExhaustedSubscriberCount);
    }

    [Fact]
    public void ReconnectCapability_WhenUnsupported_IsDiscoverableAndRejectsReset()
    {
        var connection = new ObservableConnection();
        using var client = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();

        Assert.False(client.SupportsReconnect);
        Assert.False(client.IsReconnectExhausted);
        Assert.Throws<NotSupportedException>(() => client.ResetReconnect());
    }

    [Fact]
    public void SendPacket_ForwardsEnvelopeWithoutCreatingARequest()
    {
        var connection = new ObservableConnection();
        using var client = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();

        client.SendPacket(901, Bytes(1, 2, 3), flags: 7, seq: 42);

        var send = Assert.Single(connection.Sends);
        Assert.Equal(901u, send.OpCode);
        Assert.Equal(new byte[] { 1, 2, 3 }, send.Payload);
        Assert.Equal((ushort)7, send.Flags);
        Assert.Equal(42u, send.Seq);
    }

    [Fact]
    public void PacketReceived_ForwardsEnvelopeAndUnsubscribesOnDispose()
    {
        var connection = new ObservableConnection();
        var client = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();
        uint actualOpCode = 0;
        uint actualSeq = 0;
        byte[]? actualPayload = null;
        client.PacketReceived += (opCode, seq, payload) =>
        {
            actualOpCode = opCode;
            actualSeq = seq;
            actualPayload = payload.ToArray();
        };

        Assert.Equal(2, connection.PacketReceivedSubscriberCount);

        connection.RaisePacket(902, 43, Bytes(4, 5));

        Assert.Equal(902u, actualOpCode);
        Assert.Equal(43u, actualSeq);
        Assert.Equal(new byte[] { 4, 5 }, actualPayload);

        client.Dispose();

        Assert.Equal(0, connection.PacketReceivedSubscriberCount);
    }

    [Fact]
    public async Task SendRawRequestAsync_UsesRequestEnvelopeAndCompletesFromResponse()
    {
        var connection = new ObservableConnection();
        using var client = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();

        var request = client.SendRawRequestAsync(1001, Bytes(7, 8));
        var send = Assert.Single(connection.Sends);

        Assert.Equal(1001u, send.OpCode);
        Assert.Equal(new byte[] { 7, 8 }, send.Payload);
        Assert.Equal((ushort)NetworkPacketFlags.Request, send.Flags);
        Assert.NotEqual(0u, send.Seq);

        connection.RaisePacket(send.OpCode, send.Seq, Response(TcpGatewayStatusCode.Ok, 9, 10));

        Assert.Equal(new byte[] { 9, 10 }, (await request).ToArray());
    }

    [Fact]
    public async Task Dispose_UnsubscribesFailsPendingRequestAndDisposesConnectionOnce()
    {
        var connection = new ObservableConnection();
        var client = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();
        var pending = client.SendRawRequestAsync(1101, default);

        client.Dispose();
        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
        Assert.Equal(1, connection.CloseCount);
        Assert.Equal(1, connection.DisposeCount);
        Assert.Equal(0, connection.PacketReceivedSubscriberCount);
        Assert.Equal(0, connection.DisconnectedSubscriberCount);
        Assert.Equal(0, connection.ErrorSubscriberCount);
        Assert.Equal(0, connection.ConnectedSubscriberCount);
        Assert.Equal(0, connection.ServerPushSubscriberCount);
        Assert.Equal(0, connection.KickedSubscriberCount);
    }

    [Fact]
    public void Dispose_RejectsOperationsExceptIdempotentClose()
    {
        var connection = new ObservableConnection();
        var client = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();
        client.Dispose();

        Assert.Throws<ObjectDisposedException>(() => client.Open("localhost", 7000));
        Assert.Throws<ObjectDisposedException>(() => client.Tick(0.1f));
        Assert.Throws<ObjectDisposedException>(() => client.SendPacket(1200, default));
        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = client.SendRawRequestAsync(1201, default);
        });

        client.Close();
        Assert.Equal(1, connection.CloseCount);
    }

    private static ArraySegment<byte> Bytes(params byte[] bytes) => new(bytes);

    private static ArraySegment<byte> Response(TcpGatewayStatusCode statusCode, params byte[] payload)
    {
        var bytes = new byte[sizeof(int) + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, (int)statusCode);
        payload.CopyTo(bytes, sizeof(int));
        return new ArraySegment<byte>(bytes);
    }

    private class ObservableConnection : IConnection
    {
        private Action? _connected;
        private Action? _disconnected;
        private Action<Exception>? _error;
        private Action<uint, uint, ArraySegment<byte>>? _packetReceived;
        private Action<uint, ArraySegment<byte>>? _serverPushReceived;
        private Action<string, string>? _kicked;

        public readonly List<SendRecord> Sends = new();

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public bool IsConnected => State == ConnectionState.Connected;
        public string? OpenHost { get; private set; }
        public int OpenPort { get; private set; }
        public float LastTickDelta { get; private set; }
        public int OpenCount { get; private set; }
        public int CloseCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int ConnectedSubscriberCount { get; private set; }
        public int DisconnectedSubscriberCount { get; private set; }
        public int ErrorSubscriberCount { get; private set; }
        public int PacketReceivedSubscriberCount { get; private set; }
        public int ServerPushSubscriberCount { get; private set; }
        public int KickedSubscriberCount { get; private set; }

        public event Action Connected
        {
            add { _connected += value; ConnectedSubscriberCount++; }
            remove { _connected -= value; ConnectedSubscriberCount--; }
        }

        public event Action Disconnected
        {
            add { _disconnected += value; DisconnectedSubscriberCount++; }
            remove { _disconnected -= value; DisconnectedSubscriberCount--; }
        }

        public event Action<Exception> Error
        {
            add { _error += value; ErrorSubscriberCount++; }
            remove { _error -= value; ErrorSubscriberCount--; }
        }

        public event Action<uint, uint, ArraySegment<byte>> PacketReceived
        {
            add { _packetReceived += value; PacketReceivedSubscriberCount++; }
            remove { _packetReceived -= value; PacketReceivedSubscriberCount--; }
        }

        public event Action<uint, ArraySegment<byte>> ServerPushReceived
        {
            add { _serverPushReceived += value; ServerPushSubscriberCount++; }
            remove { _serverPushReceived -= value; ServerPushSubscriberCount--; }
        }

        public event Action<string, string> Kicked
        {
            add { _kicked += value; KickedSubscriberCount++; }
            remove { _kicked -= value; KickedSubscriberCount--; }
        }

        public void Open(string host, int port)
        {
            OpenHost = host;
            OpenPort = port;
            OpenCount++;
            State = ConnectionState.Connected;
        }

        public void Close()
        {
            CloseCount++;
            State = ConnectionState.Disconnected;
        }

        public void Tick(float deltaTime)
        {
            LastTickDelta = deltaTime;
        }

        public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0)
        {
            var bytes = payload.Array == null ? Array.Empty<byte>() : payload.ToArray();
            Sends.Add(new SendRecord(opCode, seq, flags, bytes));
        }

        public void RaiseConnected()
        {
            State = ConnectionState.Connected;
            _connected?.Invoke();
        }

        public void RaiseDisconnected()
        {
            State = ConnectionState.Disconnected;
            _disconnected?.Invoke();
        }

        public void RaiseError(Exception exception) => _error?.Invoke(exception);

        public void RaisePacket(uint opCode, uint seq, ArraySegment<byte> payload) =>
            _packetReceived?.Invoke(opCode, seq, payload);

        public void RaiseServerPush(uint opCode, ArraySegment<byte> payload) =>
            _serverPushReceived?.Invoke(opCode, payload);

        public void RaiseKicked(string code, string reason) => _kicked?.Invoke(code, reason);

        public void Dispose()
        {
            DisposeCount++;
            State = ConnectionState.Disconnected;
        }

        public readonly record struct SendRecord(uint OpCode, uint Seq, ushort Flags, byte[] Payload);
    }

    private sealed class ReconnectableObservableConnection :
        ObservableConnection,
        IReconnectableConnection
    {
        private Action<int, float>? _reconnectScheduled;
        private Action<int>? _reconnectAttemptStarted;
        private Action<int>? _reconnectExhausted;

        public bool IsReconnectExhausted { get; set; }
        public int ResetReconnectCount { get; private set; }
        public int ReconnectScheduledSubscriberCount { get; private set; }
        public int ReconnectAttemptStartedSubscriberCount { get; private set; }
        public int ReconnectExhaustedSubscriberCount { get; private set; }

        public event Action<int, float> ReconnectScheduled
        {
            add { _reconnectScheduled += value; ReconnectScheduledSubscriberCount++; }
            remove { _reconnectScheduled -= value; ReconnectScheduledSubscriberCount--; }
        }

        public event Action<int> ReconnectAttemptStarted
        {
            add { _reconnectAttemptStarted += value; ReconnectAttemptStartedSubscriberCount++; }
            remove { _reconnectAttemptStarted -= value; ReconnectAttemptStartedSubscriberCount--; }
        }

        public event Action<int> ReconnectExhausted
        {
            add { _reconnectExhausted += value; ReconnectExhaustedSubscriberCount++; }
            remove { _reconnectExhausted -= value; ReconnectExhaustedSubscriberCount--; }
        }

        public void ResetReconnect()
        {
            ResetReconnectCount++;
            IsReconnectExhausted = false;
        }

        public void RaiseReconnectScheduled(int attempt, float delay) =>
            _reconnectScheduled?.Invoke(attempt, delay);

        public void RaiseReconnectAttemptStarted(int attempt) =>
            _reconnectAttemptStarted?.Invoke(attempt);

        public void RaiseReconnectExhausted(int attempts) =>
            _reconnectExhausted?.Invoke(attempts);
    }

    private sealed class ObservableTransport : ITransport
    {
        public bool IsConnected { get; private set; }
        public string? ConnectHost { get; private set; }
        public int ConnectPort { get; private set; }

        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error
        {
            add { }
            remove { }
        }

        public event Action<ArraySegment<byte>>? BytesReceived
        {
            add { }
            remove { }
        }

        public void Connect(string host, int port)
        {
            ConnectHost = host;
            ConnectPort = port;
            IsConnected = true;
            Connected?.Invoke();
        }

        public void Close()
        {
            if (!IsConnected)
            {
                return;
            }

            IsConnected = false;
            Disconnected?.Invoke();
        }

        public void Send(ArraySegment<byte> bytes)
        {
        }

        public void Dispose()
        {
            IsConnected = false;
        }
    }

    private sealed class ThrowingSubscriptionConnection : IConnection
    {
        public ConnectionState State => ConnectionState.Disconnected;
        public bool IsConnected => false;
        public int DisposeCount { get; private set; }

        public event Action Connected
        {
            add { }
            remove { }
        }

        public event Action Disconnected
        {
            add { }
            remove { }
        }

        public event Action<Exception> Error
        {
            add { }
            remove { }
        }

        public event Action<uint, uint, ArraySegment<byte>> PacketReceived
        {
            add => throw new InvalidOperationException("subscription failed");
            remove { }
        }

        public event Action<uint, ArraySegment<byte>> ServerPushReceived
        {
            add { }
            remove { }
        }

        public event Action<string, string> Kicked
        {
            add { }
            remove { }
        }

        public void Open(string host, int port)
        {
        }

        public void Close()
        {
        }

        public void Tick(float deltaTime)
        {
        }

        public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0)
        {
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
