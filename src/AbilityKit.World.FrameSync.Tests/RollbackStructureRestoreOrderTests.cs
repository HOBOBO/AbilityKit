using System.Collections;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using Xunit;

namespace AbilityKit.World.FrameSync.Tests;

public sealed class RollbackStructureRestoreOrderTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Explicit_plan_is_shared_by_preflight_and_import_without_changing_input_view(int permutation)
    {
        var events = new List<string>();
        var first = new StructureProbe(10, events);
        var second = new StructureProbe(30, events);
        var field = new Probe(20, events);
        var otherField = new Probe(5, events);
        var registry = Registry(first, field, second, otherField);
        var declared = new[] { 30, 10 };
        registry.SetStructureRestoreOrder(declared);
        declared[0] = 999;
        registry.SetStructureRestoreOrder(30, 10);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var capture = WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(new FrameIndex(7)));
        int[][] permutations = { new[] { 10, 20, 30, 5 }, new[] { 30, 20, 10, 5 }, new[] { 20, 30, 5, 10 } };
        var inputKeys = permutations[permutation];
        var snapshot = Snapshot(inputKeys);

        Assert.True(coordinator.TryRestore(snapshot, out var result), result.ToString());
        Assert.Equal(4, result.ProviderCount);
        var all = new Probe[] { first, second, field, otherField };
        Assert.All(all, p => Assert.Equal(new[] { 30, 10 }, p.ObservedPlan));
        Assert.All(all, p => Assert.Equal(inputKeys, p.ObservedInput));
        Assert.Equal(new[] { "import:30", "import:10", "import:20", "import:5" }, events.TakeLast(4));
        Assert.All(events.Take(8), e => Assert.False(e.StartsWith("import:")));
        Assert.Equal(8, events.Count(e => e.StartsWith("snapshot:") || e.StartsWith("preflight:")));
        Assert.Equal(inputKeys, snapshot.Entries.Select(e => e.Key));
        Assert.Equal(capture, WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(new FrameIndex(7))));
    }

    [Fact]
    public void Default_registry_keeps_snapshot_structure_order_and_partial_contract()
    {
        var events = new List<string>();
        var first = new StructureProbe(10, events);
        var second = new StructureProbe(30, events);
        var field = new Probe(20, events);
        var coordinator = new RollbackCoordinator(Registry(first, second, field), new RollbackSnapshotRingBuffer(2));
        Assert.True(coordinator.TryRestore(Snapshot(30, 20, 10), out _));
        Assert.Equal(new[] { "import:30", "import:10", "import:20" }, events.TakeLast(3));
        Assert.Equal(new[] { 30, 10 }, field.ObservedPlan);
        events.Clear();
        Assert.True(coordinator.TryRestore(Snapshot(20), out _));
        Assert.Empty(field.ObservedPlan);
        Assert.Equal("import:20", events.Last());
    }

    [Fact]
    public void Explicit_plan_filters_optional_entries_but_never_fills_missing_required_entries()
    {
        var events = new List<string>();
        var first = new StructureProbe(10, events);
        var second = new StructureProbe(30, events);
        var field = new Probe(20, events);
        var registry = Registry(first, second, field);
        registry.SetStructureRestoreOrder(30, 10);
        registry.RequireState(10);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));

        Assert.True(coordinator.TryRestore(Snapshot(20, 10), out _));
        Assert.Equal(new[] { 10 }, field.ObservedPlan);
        Assert.Equal(new[] { "import:10", "import:20" }, events.TakeLast(2));
        events.Clear();
        Assert.False(coordinator.TryRestore(Snapshot(20, 30), out var missing));
        Assert.Equal(10, missing.ProviderKey);
        Assert.Equal(0, missing.ProviderCount);
        Assert.Empty(events);
        Assert.False(coordinator.TryRestore(Snapshot(10, 20, 10), out var duplicate));
        Assert.Equal(0, duplicate.ProviderCount);
        Assert.Empty(events);
    }

    [Theory]
    [InlineData("duplicate")] [InlineData("missing")] [InlineData("unknown")] [InlineData("field")]
    public void Invalid_complete_plan_rejects_at_seal_without_sealing_the_registry(string defect)
    {
        var registry = Registry(new StructureProbe(10, new()), new StructureProbe(30, new()), new Probe(20, new()));
        var keys = defect switch
        {
            "duplicate" => new[] { 10, 30, 10 },
            "missing" => new[] { 10 },
            "unknown" => new[] { 10, 30, 999 },
            _ => new[] { 10, 30, 20 },
        };
        registry.SetStructureRestoreOrder(keys);
        Assert.Throws<InvalidOperationException>(() => registry.Seal());
        registry.Register(new Probe(99, new()));
        registry.Clear();
        registry.Register(new StructureProbe(1, new()));
        registry.SetStructureRestoreOrder(1);
        registry.Seal();
    }

    [Fact]
    public void Configuration_copies_input_rejects_conflicts_and_sealed_mutation_and_clears()
    {
        var registry = Registry(new StructureProbe(10, new()), new StructureProbe(30, new()));
        Assert.Throws<ArgumentNullException>(() => registry.SetStructureRestoreOrder(null!));
        registry.SetStructureRestoreOrder(30, 10);
        registry.SetStructureRestoreOrder(30, 10);
        Assert.Throws<InvalidOperationException>(() => registry.SetStructureRestoreOrder(10, 30));
        registry.Seal();
        registry.Seal();
        Assert.Throws<InvalidOperationException>(() => registry.SetStructureRestoreOrder(30, 10));
        registry.Clear();
        registry.Register(new StructureProbe(99, new()));
        registry.Seal();
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        Assert.True(coordinator.TryRestore(Snapshot(99), out _));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Preflight_rejection_prevents_every_import(bool singleProviderPreflight)
    {
        var events = new List<string>();
        var first = new StructureProbe(10, events);
        var second = new StructureProbe(30, events) { RejectSnapshot = !singleProviderPreflight, RejectPreflight = singleProviderPreflight };
        var registry = Registry(first, second);
        registry.SetStructureRestoreOrder(30, 10);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        Assert.False(coordinator.TryRestore(Snapshot(10, 30), out var result));
        Assert.Equal(30, result.ProviderKey);
        Assert.Equal(0, result.ProviderCount);
        Assert.DoesNotContain(events, e => e.StartsWith("import:"));
    }

    [Fact]
    public void Structural_failure_reports_effective_progress_and_stops_later_structure_and_fields()
    {
        var events = new List<string>();
        var first = new StructureProbe(10, events) { RejectImport = true };
        var second = new StructureProbe(30, events);
        var third = new StructureProbe(40, events);
        var registry = Registry(first, second, third, new Probe(20, events));
        registry.SetStructureRestoreOrder(30, 10, 40);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));

        Assert.False(coordinator.TryRestore(Snapshot(10, 20, 40, 30), out var result));
        Assert.Equal(RollbackOperationStatus.ProviderFailed, result.Status);
        Assert.Equal(10, result.ProviderKey);
        Assert.Equal(1, result.ProviderCount);
        Assert.Equal(new[] { "import:30", "import:10" }, events.Where(e => e.StartsWith("import:")));
    }

    [Fact]
    public void Direct_context_derives_only_snapshot_structure_members_and_does_not_expose_mutable_storage()
    {
        var events = new List<string>();
        var providers = new Dictionary<int, IRollbackStateProvider>
        {
            [10] = new StructureProbe(10, events), [30] = new StructureProbe(30, events), [20] = new Probe(20, events),
        };
        var snapshot = Snapshot(20, 30);
        var context = new RollbackSnapshotPreflightContext(snapshot.Frame, snapshot.Entries, providers);
        providers.Clear();
        Array.Reverse(snapshot.Entries);
        Assert.Equal(new[] { 30 }, context.StructureRestoreKeys);
        var mutableView = Assert.IsAssignableFrom<IList>(context.StructureRestoreKeys);
        Assert.Throws<NotSupportedException>(() => mutableView[0] = 10);
    }

    private static RollbackRegistry Registry(params IRollbackStateProvider[] providers)
    {
        var registry = new RollbackRegistry();
        foreach (var provider in providers) registry.Register(provider);
        return registry;
    }

    private static WorldRollbackSnapshot Snapshot(params int[] keys) => new(
        WorldRollbackSnapshotCodec.CurrentVersion, new FrameIndex(7),
        keys.Select(k => new WorldRollbackSnapshotEntry(k, BitConverter.GetBytes(k))).ToArray());

    private class Probe : IRollbackStateProvider, IRollbackSnapshotPreflightProvider, IRollbackStatePreflightProvider
    {
        private readonly List<string> _events;
        public Probe(int key, List<string> events) { Key = key; _events = events; }
        public int Key { get; }
        public int[] ObservedPlan { get; private set; } = Array.Empty<int>();
        public int[] ObservedInput { get; private set; } = Array.Empty<int>();
        public bool RejectSnapshot { get; init; }
        public bool RejectPreflight { get; init; }
        public bool RejectImport { get; init; }
        public byte[] Export(FrameIndex frame) => BitConverter.GetBytes(Key);
        public void ValidateSnapshot(RollbackSnapshotPreflightContext context)
        {
            _events.Add($"snapshot:{Key}");
            ObservedPlan = context.StructureRestoreKeys.ToArray();
            ObservedInput = context.Providers.Keys.ToArray();
            if (RejectSnapshot) throw new InvalidOperationException("拒绝快照");
        }
        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            _events.Add($"preflight:{Key}");
            if (RejectPreflight) throw new InvalidOperationException("拒绝单项预检");
        }
        public void Import(FrameIndex frame, byte[] payload)
        {
            Assert.Equal(BitConverter.GetBytes(Key), payload);
            _events.Add($"import:{Key}");
            if (RejectImport) throw new InvalidOperationException("注入结构导入故障");
        }
    }

    private sealed class StructureProbe : Probe, IRollbackStructureRestoreProvider
    {
        public StructureProbe(int key, List<string> events) : base(key, events) { }
    }
}
