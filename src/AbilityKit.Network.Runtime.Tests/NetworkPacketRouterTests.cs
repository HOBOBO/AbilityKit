using System;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
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
}
