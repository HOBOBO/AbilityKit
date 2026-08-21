using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Sdk;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class NetworkSdkClientHubTests
{
    [Fact]
    public void ClientKey_RequiresProjectRoleAndInstanceIdentity()
    {
        Assert.Throws<ArgumentException>(() => new NetworkSdkClientKey("", "room"));
        Assert.Throws<ArgumentException>(() => new NetworkSdkClientKey("project", ""));
        Assert.Throws<ArgumentException>(() => new NetworkSdkClientKey("project", "room", ""));

        var key = new NetworkSdkClientKey(" project ", " room ", " primary ");
        Assert.Equal("project/room/primary", key.ToString());
    }

    [Fact]
    public void Acquire_SameKeyReusesClientAndTracksLeases()
    {
        var transport = new HubTransport();
        var builds = 0;
        using var hub = new NetworkSdkClientHub();
        var key = new NetworkSdkClientKey("abilitykit.moba", "battle", "primary");
        using var first = hub.Acquire(key, () => Build(transport, ref builds));
        using var second = hub.Acquire(key, () => Build(transport, ref builds));

        Assert.Same(first.Client, second.Client);
        Assert.Equal(1, builds);
        Assert.Equal(2, hub.GetLeaseCount(key));

        first.Dispose();
        Assert.Equal(1, hub.GetLeaseCount(key));
        Assert.True(hub.TryGet(key, out var current));
        Assert.Same(second.Client, current);
    }

    [Fact]
    public void DifferentRolesDoNotShareClient()
    {
        var builds = 0;
        using var hub = new NetworkSdkClientHub();
        var room = hub.Acquire(
            new NetworkSdkClientKey("project", "room", "primary"),
            () => Build(new HubTransport(), ref builds));
        var battle = hub.Acquire(
            new NetworkSdkClientKey("project", "battle", "primary"),
            () => Build(new HubTransport(), ref builds));

        Assert.NotSame(room.Client, battle.Client);
        Assert.Equal(2, builds);
        room.Dispose();
        battle.Dispose();
    }

    [Fact]
    public void Remove_RejectsActiveLeaseThenDisposesAfterRelease()
    {
        var transport = new HubTransport();
        using var hub = new NetworkSdkClientHub();
        var key = new NetworkSdkClientKey("project", "room");
        var lease = hub.Acquire(key, () => Build(transport));
        lease.Client.Open("room.example", 7100);

        var exception = Assert.Throws<InvalidOperationException>(() => hub.Remove(key));
        Assert.Contains("active", exception.Message);
        lease.Dispose();

        Assert.True(hub.Remove(key));
        Assert.Equal(1, transport.DisposeCount);
        Assert.False(hub.TryGet(key, out _));
        Assert.Throws<ObjectDisposedException>(() => lease.Client);
    }

    [Fact]
    public void Dispose_DisposesOwnedClientsAndInvalidatesFutureOperations()
    {
        var transport = new HubTransport();
        var hub = new NetworkSdkClientHub();
        var lease = hub.Acquire(new NetworkSdkClientKey("project", "room"), () => Build(transport));
        lease.Client.Open("room.example", 7100);

        hub.Dispose();

        Assert.Equal(1, transport.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => hub.Acquire(
            new NetworkSdkClientKey("project", "room"),
            () => Build(new HubTransport())));
        lease.Dispose();
        Assert.Throws<ObjectDisposedException>(() => lease.Client);
    }

    [Fact]
    public void Acquire_ConcurrentCallersCreateOneClient()
    {
        using var hub = new NetworkSdkClientHub();
        var key = new NetworkSdkClientKey("project", "battle", "primary");
        var leases = new System.Collections.Concurrent.ConcurrentBag<NetworkSdkClientLease>();
        var builds = 0;

        Parallel.For(0, 32, _ =>
        {
            leases.Add(hub.Acquire(key, () =>
            {
                Interlocked.Increment(ref builds);
                return Build(new HubTransport());
            }));
        });

        Assert.Equal(1, builds);
        Assert.Equal(32, hub.GetLeaseCount(key));
        Assert.Single(leases.Select(item => item.Client).Distinct());
        foreach (var lease in leases) lease.Dispose();
        Assert.Equal(0, hub.GetLeaseCount(key));
    }

    [Fact]
    public void Acquire_WhenFactoryFails_DoesNotLeaveCachedEntry()
    {
        using var hub = new NetworkSdkClientHub();
        var key = new NetworkSdkClientKey("project", "room", "primary");

        Assert.Throws<InvalidOperationException>(() => hub.Acquire(
            key,
            () => throw new InvalidOperationException("build failed")));

        Assert.Equal(0, hub.Count);
        Assert.False(hub.TryGet(key, out _));
        using var lease = hub.Acquire(key, () => Build(new HubTransport()));
        Assert.Equal(1, hub.Count);
    }

    private static NetworkSdkClient Build(HubTransport transport, ref int builds)
    {
        builds++;
        return Build(transport);
    }

    private static NetworkSdkClient Build(HubTransport transport) =>
        new NetworkSdkBuilder()
            .UseTransportFactory(() => transport)
            .Build();

    private sealed class HubTransport : ITransport
    {
        public int DisposeCount { get; private set; }
        public bool IsConnected { get; private set; }
        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<ArraySegment<byte>>? BytesReceived;

        public void Connect(string host, int port) => IsConnected = true;
        public void Close() => IsConnected = false;
        public void Send(ArraySegment<byte> bytes) { }
        public void Dispose()
        {
            DisposeCount++;
            IsConnected = false;
        }
    }
}
