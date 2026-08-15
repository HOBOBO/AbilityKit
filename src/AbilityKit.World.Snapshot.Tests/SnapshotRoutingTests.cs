using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Snapshots.Routing;
using Xunit;

namespace AbilityKit.World.Snapshot.Tests;

/// <summary>
/// world.snapshot 包快照路由的直接契约测试（脱离 demo）。
/// 覆盖 SnapshotRegistryCatalog 的成功 + 失败 + 边界用例。
/// </summary>
public sealed class SnapshotRoutingTests
{
    [Fact]
    public void Add_then_TryGet_finds_by_id()
    {
        var catalog = new SnapshotRegistryCatalog();
        catalog.Add("hero", (dec, cur, stage, cmd) => { });

        Assert.True(catalog.TryGet("hero", out var registry));
        Assert.NotNull(registry);
        Assert.Equal("hero", registry.RegistryId);
        Assert.Single(catalog.Registries);
    }

    [Fact]
    public void TryGet_unknown_id_returns_false()
    {
        var catalog = new SnapshotRegistryCatalog();
        Assert.False(catalog.TryGet("missing", out var registry));
        Assert.Null(registry);
    }

    [Fact]
    public void TryGet_null_id_returns_false()
    {
        var catalog = new SnapshotRegistryCatalog();
        Assert.False(catalog.TryGet(null!, out var registry));
        Assert.Null(registry);
    }

    [Fact]
    public void Add_duplicate_id_throws()
    {
        var catalog = new SnapshotRegistryCatalog();
        catalog.Add("hero", (dec, cur, stage, cmd) => { });

        Assert.Throws<InvalidOperationException>(() =>
            catalog.Add("hero", (dec, cur, stage, cmd) => { }));
    }

    [Fact]
    public void Add_null_registry_throws()
    {
        var catalog = new SnapshotRegistryCatalog();
        Assert.Throws<ArgumentNullException>(() => catalog.Add((IIdentifiedSnapshotRegistry)null!));
    }

    [Fact]
    public void Dispatcher_subscription_stops_dispatch_after_repeated_dispose()
    {
        var dispatcher = new FrameSnapshotDispatcher();
        dispatcher.Register<int>(7, DecodeByte);
        var received = new List<int>();
        var subscription = dispatcher.Subscribe<int>(7, (_, value) => received.Add(value));

        dispatcher.Feed(new TestEnvelope(new WorldStateSnapshot(7, new byte[] { 3 })));
        subscription.Dispose();
        subscription.Dispose();
        dispatcher.Feed(new TestEnvelope(new WorldStateSnapshot(7, new byte[] { 4 })));

        Assert.Equal(new[] { 3 }, received);
    }

    [Fact]
    public void Pipeline_stage_stops_dispatch_after_repeated_dispose()
    {
        var dispatcher = new FrameSnapshotDispatcher();
        using var pipeline = new SnapshotPipeline(new object(), dispatcher);
        pipeline.Register<int>(9, DecodeByte);
        var received = new List<int>();
        var subscription = pipeline.AddStage<int>(9, 0, (_, _, value) => received.Add(value));

        dispatcher.Feed(new TestEnvelope(new WorldStateSnapshot(9, new byte[] { 5 })));
        subscription.Dispose();
        subscription.Dispose();
        dispatcher.Feed(new TestEnvelope(new WorldStateSnapshot(9, new byte[] { 6 })));

        Assert.Equal(new[] { 5 }, received);
    }

    private static bool DecodeByte(in WorldStateSnapshot snapshot, out int value)
    {
        value = snapshot.Payload[0];
        return true;
    }

    private sealed class TestEnvelope : ISnapshotEnvelope
    {
        public TestEnvelope(WorldStateSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public WorldId WorldId { get; } = new WorldId("test");
        public WorldStateSnapshot? Snapshot { get; }
    }
}
