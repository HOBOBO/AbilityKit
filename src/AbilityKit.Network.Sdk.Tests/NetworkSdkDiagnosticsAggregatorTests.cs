using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;
using AbilityKit.Network.Sdk.Diagnostics;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Catalog;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class NetworkSdkDiagnosticsAggregatorTests
{
    [Fact]
    public void ClientWithoutDiagnostics_ReturnsFalseAndAggregatesIdentity()
    {
        using var client = Build(new TestConnection());
        Assert.False(client.SupportsDiagnostics);
        Assert.False(client.TryGetDiagnosticsSnapshot(out _));

        using var hub = new NetworkSdkClientHub();
        using var lease = hub.Acquire(
            new NetworkSdkClientKey("project", "room"),
            () => Build(new TestConnection()));
        var snapshot = Assert.Single(new NetworkSdkDiagnosticsAggregator(
            hub,
            new ProtocolCatalogRegistry()).Snapshot());

        Assert.Equal("project/room/default", snapshot.Key.ToString());
        Assert.Equal(1, snapshot.LeaseCount);
        Assert.False(snapshot.SupportsDiagnostics);
        Assert.Empty(snapshot.Routes);
    }

    [Fact]
    public void HubSnapshot_IsStableAndCapturesLeaseCounts()
    {
        using var hub = new NetworkSdkClientHub();
        var key = new NetworkSdkClientKey("project", "battle", "primary");
        using var first = hub.Acquire(key, () => Build(new TestConnection()));
        using var second = hub.Acquire(key, () => Build(new TestConnection()));

        var captured = Assert.Single(hub.Snapshot());
        Assert.Equal(2, captured.LeaseCount);
        Assert.Same(first.Client, captured.Client);

        first.Dispose();
        Assert.Equal(2, captured.LeaseCount);
        Assert.Equal(1, Assert.Single(hub.Snapshot()).LeaseCount);
    }

    [Fact]
    public void Snapshot_MapsKindsPreservesCountersAndReportsMappingStates()
    {
        var connection = new ConnectionManager(() => new TestTransport());
        connection.Open("gateway.example", 7100);
        RegisterAndDispatchRoutes(connection.PacketRouter);
        using var hub = new NetworkSdkClientHub();
        using var lease = hub.Acquire(
            new NetworkSdkClientKey("project", "battle", "primary"),
            () => Build(connection));

        var catalogs = new ProtocolCatalogRegistry();
        catalogs.Register(Catalog(
            "project.battle",
            "project",
            Message(
                "send.request",
                10,
                ProtocolDirection.ClientToServer,
                ProtocolPacketKind.Request,
                responseId: "send.response"),
            Message("send.response", 20, ProtocolDirection.ServerToClient, ProtocolPacketKind.Response),
            Message("state.push", 30, ProtocolDirection.ServerToClient, ProtocolPacketKind.Push)));
        catalogs.Register(Catalog(
            "shared.room",
            NetworkSdkDiagnosticsAggregator.SharedCatalogProjectId,
            Message("shared-state.push", 30, ProtocolDirection.ServerToClient, ProtocolPacketKind.Push)));

        var client = Assert.Single(new NetworkSdkDiagnosticsAggregator(hub, catalogs).Snapshot());
        Assert.True(client.SupportsDiagnostics);
        Assert.Equal(4, client.Routes.Count);

        var request = Assert.Single(client.Routes, route => route.Route.OpCode == 10);
        Assert.Equal(ProtocolDirection.ClientToServer, request.Direction);
        Assert.Equal(ProtocolPacketKind.Request, request.PacketKind);
        Assert.Equal(NetworkRouteCatalogMappingStatus.Mapped, request.MappingStatus);
        Assert.Equal("send.request", Assert.Single(request.Candidates).MessageId);

        var response = Assert.Single(client.Routes, route => route.Route.OpCode == 20);
        Assert.Equal(ProtocolPacketKind.Response, response.PacketKind);
        Assert.Equal(NetworkRouteCatalogMappingStatus.Mapped, response.MappingStatus);

        var push = Assert.Single(client.Routes, route => route.Route.OpCode == 30);
        Assert.Equal(ProtocolPacketKind.Push, push.PacketKind);
        Assert.Equal(NetworkRouteCatalogMappingStatus.Ambiguous, push.MappingStatus);
        Assert.Equal(2, push.Candidates.Count);
        Assert.Equal(2, push.Route.HandlerCount);
        Assert.Equal(1, push.Route.DispatchCount);
        Assert.Equal(1, push.Route.HandledCount);
        Assert.Equal(1, push.Route.ExceptionCount);

        var unresolved = Assert.Single(client.Routes, route => route.Route.OpCode == 40);
        Assert.Equal(NetworkRouteCatalogMappingStatus.Unresolved, unresolved.MappingStatus);
    }

    [Fact]
    public void ClientDiagnostics_AfterDisposeThrows()
    {
        var client = Build(new TestConnection());
        client.Dispose();
        Assert.Throws<ObjectDisposedException>(() => client.TryGetDiagnosticsSnapshot(out _));
    }

    private static NetworkSdkClient Build(IConnection connection) => new NetworkSdkBuilder()
        .UseConnectionFactory(() => connection)
        .Build();

    private static ProtocolCatalogDefinition Catalog(
        string catalogId,
        string projectId,
        params ProtocolMessageDefinition[] messages) =>
        new(catalogId, projectId, "battle", 1, "memorypack", messages);

    private static ProtocolMessageDefinition Message(
        string id,
        uint opCode,
        ProtocolDirection direction,
        ProtocolPacketKind kind,
        string? responseId = null) =>
        new(id, opCode, direction, kind, "Payload", "memorypack", responseId: responseId);

    private class TestConnection : IConnection
    {
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public bool IsConnected => State == ConnectionState.Connected;
        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<uint, uint, ArraySegment<byte>>? PacketReceived;
        public event Action<uint, ArraySegment<byte>>? ServerPushReceived;
        public event Action<string, string>? Kicked;

        public void Open(string host, int port) { State = ConnectionState.Connected; Connected?.Invoke(); }
        public void Close() { State = ConnectionState.Disconnected; Disconnected?.Invoke(); }
        public void Tick(float deltaTime) { }
        public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0) { }
        public void Dispose() => Close();
    }

    private static void RegisterAndDispatchRoutes(NetworkPacketRouter router)
    {
        router.Register(10, NetworkPacketDispatchKind.Request, _ => { });
        router.Register(20, NetworkPacketDispatchKind.Response, _ => { });
        router.Register(30, NetworkPacketDispatchKind.ServerPush, _ => { });
        router.Register(30, NetworkPacketDispatchKind.ServerPush, _ => throw new InvalidOperationException());
        router.Register(40, NetworkPacketDispatchKind.ServerPush, _ => { });
        Dispatch(router, 10, NetworkPacketFlags.Request);
        Dispatch(router, 20, NetworkPacketFlags.Response);
        Dispatch(router, 30, NetworkPacketFlags.ServerPush);
    }

    private static void Dispatch(NetworkPacketRouter router, uint opCode, NetworkPacketFlags flags) =>
        router.Dispatch(
            new NetworkPacketHeader(flags, opCode, 1, 0),
            new ArraySegment<byte>(Array.Empty<byte>()));

    private sealed class TestTransport : ITransport
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

        public void Close()
        {
            if (!IsConnected) return;
            IsConnected = false;
            Disconnected?.Invoke();
        }

        public void Send(ArraySegment<byte> bytes) { }
        public void Dispose() => Close();
    }
}
