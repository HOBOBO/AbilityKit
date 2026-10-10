using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class AbilityKitConnectionRegistryTests
{
    private static readonly AbilityKitConnectionRole Role = AbilityKitConnectionRole.GatewayReliable;

    [Fact]
    public void Acquire_RejectsDifferentEndpointForSameRole()
    {
        using var registry = new AbilityKitConnectionRegistry();
        using var lease = registry.Acquire(Descriptor("room-a", 4000), _ => new TestConnection());

        Assert.Throws<InvalidOperationException>(() =>
            registry.Acquire(Descriptor("room-b", 4000), _ => new TestConnection()));
        Assert.Same(lease.Connection, registry.GetRequired(Role));
    }

    [Fact]
    public void Acquire_BorrowerDoesNotOwnLifecycleAndOwnerReleaseWaitsForBorrower()
    {
        using var registry = new AbilityKitConnectionRegistry();
        var connection = new TestConnection();
        var owner = registry.Acquire(Descriptor("room-a", 4000), _ => connection);
        var borrower = registry.Acquire(Descriptor("room-a", 4000), _ => throw new Exception());

        Assert.True(owner.OwnsLifecycle);
        Assert.False(borrower.OwnsLifecycle);
        owner.Dispose();
        Assert.Equal(0, connection.DisposeCount);
        Assert.Throws<InvalidOperationException>(() =>
            registry.Acquire(Descriptor("room-a", 4000), _ => new TestConnection()));

        borrower.Dispose();
        Assert.Equal(1, connection.DisposeCount);
        Assert.False(registry.TryGet(Role, out _));
    }

    [Fact]
    public void StaleLeaseCannotRemoveReplacementGeneration()
    {
        using var registry = new AbilityKitConnectionRegistry();
        var stale = registry.Acquire(Descriptor("room-a", 4000), _ => new TestConnection());
        var replacement = new TestConnection();
        registry.Register(Role, replacement);

        stale.Dispose();

        Assert.Same(replacement, registry.GetRequired(Role));
        Assert.Equal(0, replacement.DisposeCount);
    }

    [Fact]
    public void RegisteringSameConnection_PreservesActiveLease()
    {
        using var registry = new AbilityKitConnectionRegistry();
        var connection = new TestConnection();
        var lease = registry.Acquire(Descriptor("room-a", 4000), _ => connection);

        registry.Register(Role, connection);
        lease.Dispose();

        Assert.Equal(1, connection.DisposeCount);
        Assert.False(registry.TryGet(Role, out _));
    }

    [Fact]
    public void ReplacingConnection_DefersOldDisposalUntilAllLeasesEnd()
    {
        using var registry = new AbilityKitConnectionRegistry();
        var old = new TestConnection();
        var owner = registry.Acquire(Descriptor("room-a", 4000), _ => old);
        var borrower = registry.Acquire(Descriptor("room-a", 4000), _ => throw new Exception());
        var replacement = new TestConnection();

        registry.Register(Role, replacement);
        owner.Dispose();
        Assert.Equal(0, old.DisposeCount);

        borrower.Dispose();
        Assert.Equal(1, old.DisposeCount);
        Assert.Same(replacement, registry.GetRequired(Role));
    }

    [Fact]
    public void RemovingConnection_DefersDisposalUntilLeaseEnds()
    {
        using var registry = new AbilityKitConnectionRegistry();
        var connection = new TestConnection();
        var lease = registry.Acquire(Descriptor("room-a", 4000), _ => connection);

        Assert.True(registry.Remove(Role));
        Assert.Equal(0, connection.DisposeCount);
        lease.Dispose();
        Assert.Equal(1, connection.DisposeCount);
    }

    [Fact]
    public void ReplacingWithoutDisposal_LeavesLeasedConnectionAlive()
    {
        using var registry = new AbilityKitConnectionRegistry();
        var connection = new TestConnection();
        var lease = registry.Acquire(Descriptor("room-a", 4000), _ => connection);

        registry.Register(Role, new TestConnection(), disposeOnReplace: false);
        lease.Dispose();

        Assert.Equal(0, connection.DisposeCount);
    }

    [Fact]
    public void DisposingRegistryBeforeLease_DoesNotDisposeConnectionTwice()
    {
        var registry = new AbilityKitConnectionRegistry();
        var connection = new TestConnection();
        var lease = registry.Acquire(Descriptor("room-a", 4000), _ => connection);

        registry.Dispose();
        lease.Dispose();

        Assert.Equal(1, connection.DisposeCount);
    }

    [Fact]
    public void GetOrCreate_RejectsDifferentEndpointForSameRole()
    {
        using var registry = new AbilityKitConnectionRegistry();
        registry.GetOrCreate(Descriptor("room-a", 4000), _ => new TestConnection());

        Assert.Throws<InvalidOperationException>(() =>
            registry.GetOrCreate(Descriptor("room-a", 4001), _ => new TestConnection()));
    }

    private static AbilityKitConnectionDescriptor Descriptor(string host, int port) =>
        new(Role, host, port, "tcp");

    private sealed class TestConnection : IConnection
    {
        public ConnectionState State => ConnectionState.Disconnected;
        public bool IsConnected => false;
        public int DisposeCount { get; private set; }
        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<uint, uint, ArraySegment<byte>>? PacketReceived;
        public event Action<uint, ArraySegment<byte>>? ServerPushReceived;
        public event Action<string, string>? Kicked;
        public void Open(string host, int port) { }
        public void Close() { }
        public void Tick(float deltaTime) { }
        public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0) { }
        public void Dispose() => DisposeCount++;
    }
}
