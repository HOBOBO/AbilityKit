using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Snapshots.Routing;
using Xunit;

namespace AbilityKit.World.Snapshot.Tests;

/// <summary>
/// world.snapshot 包快照路由的直接契约测试（脱离 demo）。
/// 覆盖注册目录、路由装配和订阅的生命周期。
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

    [Fact]
    public void Dispatcher_disposes_the_exact_registration_when_a_handler_is_registered_twice()
    {
        using var dispatcher = new FrameSnapshotDispatcher();
        dispatcher.Register<int>(7, DecodeByte);
        var received = new List<int>();
        Action<ISnapshotEnvelope, int> repeated = (_, value) => received.Add(value);
        using var first = dispatcher.Subscribe(7, repeated);
        using var middle = dispatcher.Subscribe<int>(7, (_, value) => received.Add(-value));
        using var last = dispatcher.Subscribe(7, repeated);
        var envelope = new TestEnvelope(new WorldStateSnapshot(7, new byte[] { 3 }));

        last.Dispose();
        last.Dispose();
        dispatcher.Feed(envelope);
        Assert.Equal(new[] { 3, -3 }, received);

        received.Clear();
        first.Dispose();
        dispatcher.Feed(envelope);
        Assert.Equal(new[] { -3 }, received);
    }

    [Fact]
    public void Pipeline_disposes_the_exact_registration_within_the_same_stage_order()
    {
        using var dispatcher = new FrameSnapshotDispatcher();
        using var pipeline = new SnapshotPipeline(new object(), dispatcher);
        pipeline.Register<int>(9, DecodeByte);
        var received = new List<int>();
        Action<object, ISnapshotEnvelope, int> repeated = (_, _, value) => received.Add(value);
        using var first = pipeline.AddStage(9, 0, repeated);
        using var middle = pipeline.AddStage<int>(9, 0, (_, _, value) => received.Add(-value));
        using var last = pipeline.AddStage(9, 0, repeated);
        using var earlierStage = pipeline.AddStage<int>(9, -1, (_, _, _) => received.Add(0));
        var envelope = new TestEnvelope(new WorldStateSnapshot(9, new byte[] { 5 }));

        last.Dispose();
        last.Dispose();
        dispatcher.Feed(envelope);
        Assert.Equal(new[] { 0, 5, -5 }, received);

        received.Clear();
        first.Dispose();
        dispatcher.Feed(envelope);
        Assert.Equal(new[] { 0, -5 }, received);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_failure_detaches_only_owned_subscriptions_from_external_dispatcher(bool filterRegistries)
    {
        const int opCode = 9;
        using var dispatcher = new FrameSnapshotDispatcher();
        dispatcher.Register<int>(opCode, DecodeByte);

        var received = new List<string>();
        dispatcher.SnapshotReceived += (_, _) => received.Add("external-event");
        using var existing = dispatcher.Subscribe<int>(opCode, (_, value) => received.Add($"external-{value}"));
        var registerError = new InvalidOperationException("registry failed after subscribing");
        var registries = new ISnapshotRegistry[]
        {
            SnapshotRoutingBuilder.From("partial", (decoders, pipelineDecoders, stages, commands) =>
            {
                decoders.RegisterDecoder<int>(opCode, DecodeIncrementedByte);
                pipelineDecoders.RegisterDecoder<int>(opCode, DecodeByte);
                stages.AddPipelineStage<int>(opCode, 0, (_, _, _) => received.Add("owned-stage"));
                commands.RegisterCmdHandler<int>(opCode, (_, _, _) => received.Add("owned-command"));
                throw registerError;
            })
        };

        var failure = Assert.Throws<InvalidOperationException>(() => filterRegistries
            ? SnapshotRoutingBuilder.Build(new object(), dispatcher, registries, new HashSet<string> { "partial" })
            : SnapshotRoutingBuilder.Build(new object(), dispatcher, registries));

        Assert.Same(registerError, failure);
        dispatcher.Feed(new TestEnvelope(new WorldStateSnapshot(opCode, new byte[] { 5 })));
        Assert.Equal(new[] { "external-event", "external-6" }, received);
    }

    [Fact]
    public void Build_failure_during_registry_enumeration_detaches_owned_dispatcher_callbacks()
    {
        const int opCode = 9;
        var received = new List<string>();
        var enumerationError = new InvalidOperationException("registry enumeration failed");
        FrameSnapshotDispatcher? dispatcher = null;

        IEnumerable<ISnapshotRegistry> Registries()
        {
            yield return SnapshotRoutingBuilder.From((decoders, pipelineDecoders, stages, commands) =>
            {
                dispatcher = Assert.IsType<FrameSnapshotDispatcher>(decoders);
                decoders.RegisterDecoder<int>(opCode, DecodeByte);
                pipelineDecoders.RegisterDecoder<int>(opCode, DecodeByte);
                stages.AddPipelineStage<int>(opCode, 0, (_, _, _) => received.Add("owned-stage"));
                commands.RegisterCmdHandler<int>(opCode, (_, _, _) => received.Add("owned-command"));
            });
            throw enumerationError;
        }

        var failure = Assert.Throws<InvalidOperationException>(() => SnapshotRoutingBuilder.Build(new object(), Registries()));

        Assert.Same(enumerationError, failure);
        Assert.NotNull(dispatcher);
        dispatcher.Feed(new TestEnvelope(new WorldStateSnapshot(opCode, new byte[] { 5 })));
        Assert.Empty(received);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_filters_identified_registries_without_reordering_or_excluding_anonymous_registries(bool useExternalDispatcher)
    {
        using var dispatcher = new FrameSnapshotDispatcher();
        var registered = new List<string>();
        var registries = new[]
        {
            SnapshotRoutingBuilder.From("enabled", (_, _, _, _) => registered.Add("enabled")),
            null!,
            SnapshotRoutingBuilder.From("disabled", (_, _, _, _) => registered.Add("disabled")),
            SnapshotRoutingBuilder.From((_, _, _, _) => registered.Add("anonymous"))
        };
        var enabledIds = new HashSet<string> { "enabled" };

        using var routing = useExternalDispatcher
            ? SnapshotRoutingBuilder.Build(new object(), dispatcher, registries, enabledIds)
            : SnapshotRoutingBuilder.Build(new object(), registries, enabledIds);

        Assert.Equal(new[] { "enabled", "anonymous" }, registered);
        if (useExternalDispatcher)
        {
            Assert.Same(dispatcher, routing.ExternalDispatcher);
            Assert.Null(routing.Snapshots);
        }
        else
        {
            Assert.Null(routing.ExternalDispatcher);
            Assert.NotNull(routing.Snapshots);
        }
    }

    private static bool DecodeIncrementedByte(in WorldStateSnapshot snapshot, out int value)
    {
        value = snapshot.Payload[0] + 1;
        return true;
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
