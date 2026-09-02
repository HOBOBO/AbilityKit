using System;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Catalog;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class NetworkPacketRouterTests
{
    [Fact]
    public void Dispatch_InvokesRegisteredHandlerAndPublishesSnapshot()
    {
        var router = new NetworkPacketRouter();
        var handled = 0;
        router.Register(10, NetworkPacketDispatchKind.Request, dispatch =>
        {
            Assert.Equal(NetworkPacketDispatchKind.Request, dispatch.Kind);
            Assert.Equal(10u, dispatch.Header.OpCode);
            handled++;
        });

        var dispatched = router.Dispatch(
            new NetworkPacketHeader(NetworkPacketFlags.Request, 10, 3, 2),
            new ArraySegment<byte>(new byte[] { 1, 2 }));

        Assert.True(dispatched);
        Assert.Equal(1, handled);
        var snapshot = router.GetSnapshot();
        Assert.Equal(1, snapshot.DispatchedCount);
        Assert.Equal(1, snapshot.HandledCount);
        Assert.Equal(0, snapshot.UnknownCount);
        Assert.Single(snapshot.Routes);
        Assert.Equal(1, snapshot.Routes[0].DispatchCount);
        Assert.Equal(1, snapshot.Routes[0].HandledCount);
    }

    [Fact]
    public void Dispatch_SeparatesPushAndUnknownRoutes()
    {
        var router = new NetworkPacketRouter();
        var pushes = 0;
        router.Register(20, NetworkPacketDispatchKind.ServerPush, _ => pushes++);

        Assert.True(router.Dispatch(
            new NetworkPacketHeader(NetworkPacketFlags.ServerPush, 20, 0, 0), default));
        Assert.False(router.Dispatch(
            new NetworkPacketHeader(NetworkPacketFlags.Response, 20, 1, 0), default));

        var snapshot = router.GetSnapshot();
        Assert.Equal(2, snapshot.DispatchedCount);
        Assert.Equal(1, snapshot.HandledCount);
        Assert.Equal(1, snapshot.UnknownCount);
        Assert.Equal(1, pushes);
    }

    [Fact]
    public void Dispatch_IsolatesHandlerExceptionsAndContinuesOtherHandlers()
    {
        var reported = 0;
        var router = new NetworkPacketRouter(_ => reported++);
        var completed = 0;
        router.Register(30, NetworkPacketDispatchKind.Response, _ => throw new InvalidOperationException("handler"));
        router.Register(30, NetworkPacketDispatchKind.Response, _ => completed++);

        Assert.True(router.Dispatch(
            new NetworkPacketHeader(NetworkPacketFlags.Response, 30, 1, 0), default));

        var snapshot = router.GetSnapshot();
        Assert.Equal(1, completed);
        Assert.Equal(1, reported);
        Assert.Equal(1, snapshot.ExceptionCount);
        Assert.Equal(1, snapshot.Routes[0].ExceptionCount);
        Assert.Equal(1, snapshot.HandledCount);
    }

    [Fact]
    public void Register_IsIdempotentAndUnregisterRemovesHandler()
    {
        var router = new NetworkPacketRouter();
        var handled = 0;
        NetworkPacketRouteHandler handler = _ => handled++;
        router.Register(40, NetworkPacketDispatchKind.ServerPush, handler);
        router.Register(40, NetworkPacketDispatchKind.ServerPush, handler);

        router.Dispatch(new NetworkPacketHeader(NetworkPacketFlags.ServerPush, 40, 0, 0), default);
        Assert.Equal(1, handled);
        Assert.True(router.Unregister(40, NetworkPacketDispatchKind.ServerPush, handler));
        Assert.False(router.Dispatch(new NetworkPacketHeader(NetworkPacketFlags.ServerPush, 40, 0, 0), default));
    }

    [Fact]
    public void Dispatch_BoundaryRejectsOversizedAndMalformedPayloadBeforeHandlers()
    {
        var catalogs = new ProtocolCatalogRegistry();
        catalogs.Register(new ProtocolCatalogDefinition(
            "project-a.room", "project-a", "room", 1, "memorypack",
            new[]
            {
                new ProtocolMessageDefinition(
                    "room.login", 52, ProtocolDirection.ClientToServer,
                    ProtocolPacketKind.Request, "Payload", "memorypack"),
                new ProtocolMessageDefinition(
                    "room.push", 50, ProtocolDirection.ServerToClient,
                    ProtocolPacketKind.Push, "Payload", "memorypack",
                    maximumPayloadBytes: 2)
            }));

        var failures = new List<ProtocolPacketBoundaryFailureKind>();
        var boundary = new ProtocolPacketBoundaryValidator(
            catalogs, "project-a.room", failure: (kind, _) => failures.Add(kind));
        var router = new NetworkPacketRouter(boundaryValidator: boundary.Validate);
        var handled = 0;
        router.Register(50, NetworkPacketDispatchKind.ServerPush, _ => handled++);

        Assert.False(router.Dispatch(
            new NetworkPacketHeader(NetworkPacketFlags.ServerPush, 50, 0, 3),
            new ArraySegment<byte>(new byte[] { 1, 2, 3 })));
        Assert.False(router.Dispatch(
            new NetworkPacketHeader(NetworkPacketFlags.ServerPush, 50, 0, 2),
            new ArraySegment<byte>(new byte[] { 1 })));
        Assert.Equal(0, handled);
        Assert.Equal(new[]
        {
            ProtocolPacketBoundaryFailureKind.PayloadTooLarge,
            ProtocolPacketBoundaryFailureKind.MalformedPayloadLength
        }, failures);
        Assert.Equal(2, router.GetSnapshot().DispatchedCount);
        Assert.Equal(2, router.GetSnapshot().BoundaryRejectedCount);
    }

    [Fact]
    public void Dispatch_BoundaryCanRequireCatalogNegotiation()
    {
        var catalogs = new ProtocolCatalogRegistry();
        var catalog = new ProtocolCatalogDefinition(
            "project-a.room", "project-a", "room", 1, "memorypack",
            new[]
            {
                new ProtocolMessageDefinition(
                    "room.push", 51, ProtocolDirection.ServerToClient,
                    ProtocolPacketKind.Push, "Payload", "memorypack",
                    minimumSchemaVersion: 1,
                    maximumSchemaVersion: 3)
            });
        catalogs.Register(catalog);
        var negotiation = new ProtocolCatalogNegotiationSession(catalog);
        var failures = new List<ProtocolPacketBoundaryFailureKind>();
        var boundary = new ProtocolPacketBoundaryValidator(
            catalogs,
            "project-a.room",
            failure: (kind, _) => failures.Add(kind),
            negotiationSession: negotiation,
            requireNegotiated: true,
            bootstrapMessageIds: new[] { "room.login" });
        var router = new NetworkPacketRouter(boundaryValidator: boundary.Validate);
        var handled = 0;
        router.Register(51, NetworkPacketDispatchKind.ServerPush, _ => handled++);
        var header = new NetworkPacketHeader(NetworkPacketFlags.ServerPush, 51, 0, 1);

        Assert.False(router.Dispatch(header, new ArraySegment<byte>(new byte[] { 1 })));
        Assert.True(boundary.Validate(
            new NetworkPacketHeader(NetworkPacketFlags.Request, 52, 0, 1),
            new ArraySegment<byte>(new byte[] { 1 })));
        negotiation.ApplyRemoteCatalog(catalog);
        Assert.True(router.Dispatch(header, new ArraySegment<byte>(new byte[] { 1 })));
        Assert.Equal(1, handled);
        Assert.Contains(ProtocolPacketBoundaryFailureKind.NegotiationPending, failures);
    }
}
