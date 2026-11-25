using System;
using System.Collections.Concurrent;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.TcpGateway;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class NetworkProtocolSessionTests
{
    [Fact]
    public void RoutedConnection_RegistersPushHandlerIdempotentlyAndCopiesPayload()
    {
        var connection = new RoutedTestConnection();
        using var gateway = new NetworkProtocolSession(connection, TcpGatewayRequestResponseEnvelopeCodec.Instance);
        var calls = 0;
        byte[]? received = null;
        Action<byte[]> handler = payload =>
        {
            calls++;
            received = payload;
        };

        gateway.RegisterPushHandler(1001, handler);
        gateway.RegisterPushHandler(1001, handler);

        var source = new byte[] { 1, 2, 3 };
        connection.PacketRouter.Dispatch(
            Header(NetworkPacketFlags.ServerPush, 1001),
            new ArraySegment<byte>(source));
        source[0] = 9;

        Assert.Equal(1, calls);
        Assert.Equal(new byte[] { 1, 2, 3 }, received);
        var snapshot = connection.PacketRouter.GetSnapshot();
        var route = Assert.Single(snapshot.Routes);
        Assert.Equal(1, route.HandlerCount);
    }

    [Fact]
    public void RoutedConnection_UnregisterStopsHandlerAndDisposeRemovesRoutes()
    {
        var connection = new RoutedTestConnection();
        var gateway = new NetworkProtocolSession(connection, TcpGatewayRequestResponseEnvelopeCodec.Instance);
        var calls = 0;
        Action<byte[]> handler = _ => calls++;

        gateway.RegisterPushHandler(1002, handler);
        gateway.UnregisterPushHandler(1002, handler);
        connection.DispatchPush(1002, new byte[] { 1 });
        Assert.Equal(0, calls);
        Assert.Empty(connection.PacketRouter.GetSnapshot().Routes);

        gateway.RegisterPushHandler(1003, handler);
        Assert.Single(connection.PacketRouter.GetSnapshot().Routes);
        gateway.Dispose();
        connection.DispatchPush(1003, new byte[] { 2 });

        Assert.Equal(0, calls);
        Assert.Empty(connection.PacketRouter.GetSnapshot().Routes);
    }

    [Fact]
    public void RoutedConnection_HandlerExceptionDoesNotBlockOtherHandlersAndIsCounted()
    {
        var connection = new RoutedTestConnection();
        using var gateway = new NetworkProtocolSession(connection, TcpGatewayRequestResponseEnvelopeCodec.Instance);
        var calls = 0;
        Action<byte[]> throwing = _ => throw new InvalidOperationException("handler failed");
        Action<byte[]> succeeding = _ => calls++;

        gateway.RegisterPushHandler(1004, throwing);
        gateway.RegisterPushHandler(1004, succeeding);

        connection.DispatchPush(1004, new byte[] { 7 });

        Assert.Equal(1, calls);
        var snapshot = connection.PacketRouter.GetSnapshot();
        Assert.Equal(1, snapshot.ExceptionCount);
        Assert.Equal(1, snapshot.HandledCount);
    }

    [Fact]
    public void LegacyConnection_UsesServerPushEventFallback()
    {
        var connection = new LegacyTestConnection();
        using var gateway = new NetworkProtocolSession(connection, TcpGatewayRequestResponseEnvelopeCodec.Instance);
        byte[]? received = null;
        gateway.RegisterPushHandler(1005, payload => received = payload);

        var source = new byte[] { 4, 5 };
        connection.RaiseServerPush(1005, new ArraySegment<byte>(source));
        source[0] = 8;

        Assert.Equal(new byte[] { 4, 5 }, received);
    }

    [Fact]
    public void ServerPushSubscribers_IsolateExceptionsAndContinueDispatch()
    {
        var connection = new LegacyTestConnection();
        using var gateway = new NetworkProtocolSession(connection, TcpGatewayRequestResponseEnvelopeCodec.Instance);
        var subscriberCalls = 0;
        var handlerCalls = 0;

        gateway.ServerPushReceived += (_, _) =>
            throw new InvalidOperationException("subscriber failed");
        gateway.ServerPushReceived += (_, _) => subscriberCalls++;
        gateway.RegisterPushHandler(1006, _ => handlerCalls++);

        connection.RaiseServerPush(1006, new ArraySegment<byte>(new byte[] { 1 }));

        Assert.Equal(1, subscriberCalls);
        Assert.Equal(1, handlerCalls);
    }

    [Fact]
    public void DisposeInServerPushSubscriber_SkipsRemainingCallbacksAndKeepsConnectionOpen()
    {
        var connection = new LegacyTestConnection();
        using var session = new NetworkProtocolSession(connection);
        var calls = 0;
        Action<uint, ArraySegment<byte>> later = (_, _) => calls++;
        session.ServerPushReceived += (_, _) => session.Dispose();
        session.ServerPushReceived += later;
        session.RegisterPushHandler(1007, _ => calls++);

        connection.RaiseServerPush(1007, new ArraySegment<byte>(new byte[] { 1 }));

        Assert.Equal(0, calls);
        Assert.False(session.IsConnected);
        Assert.True(connection.IsConnected);
        Assert.Same(connection, session.RawConnection);
        Assert.Throws<ObjectDisposedException>(() => session.ServerPushReceived += later);
        Assert.Throws<ObjectDisposedException>(() => session.RegisterPushHandler(1007, _ => calls++));
        session.ServerPushReceived -= later;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisposeInPushHandler_SkipsRemainingHandlersFromTheSnapshot(bool routed)
    {
        TestConnectionBase connection = routed ? new RoutedTestConnection() : new LegacyTestConnection();
        using var session = new NetworkProtocolSession(connection);
        var calls = 0;
        session.RegisterPushHandler(1008, _ => session.Dispose());
        session.RegisterPushHandler(1008, _ => calls++);

        if (connection is RoutedTestConnection routedConnection)
        {
            routedConnection.DispatchPush(1008, new byte[] { 1 });
            var route = Assert.Single(routedConnection.PacketRouter.GetSnapshot().Routes);
            Assert.Equal(0, route.HandlerCount);
        }
        else
        {
            ((LegacyTestConnection)connection).RaiseServerPush(1008, new ArraySegment<byte>(new byte[] { 1 }));
        }

        Assert.Equal(0, calls);
        Assert.True(connection.IsConnected);
    }

    private static NetworkPacketHeader Header(NetworkPacketFlags flags, uint opCode) =>
        new(flags, opCode, seq: 0, payloadLength: 0);

    private abstract class TestConnectionBase : IConnection
    {
        public ConnectionState State { get; protected set; } = ConnectionState.Connected;
        public bool IsConnected => State == ConnectionState.Connected;

        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<uint, uint, ArraySegment<byte>>? PacketReceived;
        public event Action<uint, ArraySegment<byte>>? ServerPushReceived;
        public event Action<string, string>? Kicked;

        public void Open(string host, int port)
        {
            State = ConnectionState.Connected;
            Connected?.Invoke();
        }

        public void Close()
        {
            State = ConnectionState.Disconnected;
            Disconnected?.Invoke();
        }

        public void Tick(float deltaTime)
        {
        }

        public void Send(
            uint opCode,
            ArraySegment<byte> payload,
            ushort flags = 0,
            uint seq = 0)
        {
        }

        public void Dispose()
        {
            State = ConnectionState.Disconnected;
        }

        protected void RaiseServerPush(uint opCode, ArraySegment<byte> payload) =>
            ServerPushReceived?.Invoke(opCode, payload);
    }

    private sealed class RoutedTestConnection : TestConnectionBase, IProtocolRoutedConnection
    {
        public NetworkPacketRouter PacketRouter { get; } = new();

        public void DispatchPush(uint opCode, byte[] payload) =>
            PacketRouter.Dispatch(
                Header(NetworkPacketFlags.ServerPush, opCode),
                new ArraySegment<byte>(payload));
    }

    private sealed class LegacyTestConnection : TestConnectionBase
    {
        public void RaiseServerPush(uint opCode, ArraySegment<byte> payload) =>
            base.RaiseServerPush(opCode, payload);
    }
}
