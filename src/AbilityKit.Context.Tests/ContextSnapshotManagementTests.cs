using AbilityKit.Context;
using Xunit;

namespace AbilityKit.Context.Tests;

public sealed class ContextSnapshotManagementTests
{
    [Fact]
    public void Registration_is_explicit_stable_and_rejects_conflicts()
    {
        var store = new SnapshotStorage();
        Assert.Throws<InvalidOperationException>(() => Save(store));
        var type = store.RegisterType<ValueSnapshot>("test.value", 2);
        Assert.Same(type, store.RegisterType<ValueSnapshot>("test.value", 2));
        Assert.Throws<InvalidOperationException>(() => store.RegisterType<ValueSnapshot>("test.value", 3));
        Assert.Throws<InvalidOperationException>(() => store.RegisterType<OtherSnapshot>("test.value", 2));
        Assert.Single(store.GetSnapshotTypes());
        var reference = Save(store);
        Assert.Equal(ContextSnapshotQueryStatus.Found, store.Query(reference, out var record));
        Assert.Equal("test.value", record.TypeId);
        Assert.Equal(2, record.SchemaVersion);
        Assert.Equal(7, record.Version);
        Assert.Equal(1, record.Reference.Generation);
        Assert.Equal(ContextSnapshotPurpose.Observation, record.Purpose);
        Assert.Throws<InvalidOperationException>(() => store.Save(new ValueSnapshot(1, 1, 99)));
    }

    [Fact]
    public void Historical_reads_never_fall_back_to_latest_or_realtime()
    {
        var store = Create();
        var first = Save(store, frame: 10, value: 42);
        var second = Save(store, frame: 20, value: 99, kind: "removed");
        var resolver = new ContextValueResolver(new ContextRegistry(), store);
        var result = resolver.ReadSnapshot<ValueSnapshot, int>(first, "value");
        Assert.True(result.Found);
        Assert.Equal(42, result.Value);
        Assert.Equal(10, result.Record.Frame);
        Assert.Equal(99, store.ReadSnapshot<ValueSnapshot, int>(second, "value").Value);
        Assert.Equal(ContextSnapshotQueryStatus.FieldMissing, store.ReadSnapshot<ValueSnapshot, int>(first, "absent").Status);
        Assert.Equal(ContextSnapshotQueryStatus.TypeMismatch, store.ReadSnapshot<OtherSnapshot, int>(first, "value").Status);
        Assert.True(store.TryGetLatest<ValueSnapshot>(1, 1, "captured", out var captured));
        Assert.Equal(first.SnapshotId, captured.Reference.SnapshotId);
    }

    [Fact]
    public void Identity_separates_generations_and_stores()
    {
        var store = Create();
        var old = Save(store, generation: 1, value: 10);
        var current = Save(store, generation: 2, value: 20);
        Assert.Single(store.GetHistory(1, 1));
        Assert.Single(store.GetHistory(1, 2));
        Assert.Equal(10, store.ReadSnapshot<ValueSnapshot, int>(old, "value").Value);
        Assert.Equal(20, store.ReadSnapshot<ValueSnapshot, int>(current, "value").Value);
        var wrongGeneration = new ContextSnapshotReference(store.StorageId, old.SnapshotId, 1, 2);
        Assert.Equal(ContextSnapshotQueryStatus.IdentityMismatch, store.Query(wrongGeneration, out _));
        Assert.Equal(ContextSnapshotQueryStatus.IdentityMismatch, Create().Query(old, out _));
        Assert.Equal(ContextSnapshotQueryStatus.InvalidReference, store.Query(default, out _));
        var future = new ContextSnapshotReference(store.StorageId, current.SnapshotId + 1, 1, 2);
        Assert.Equal(ContextSnapshotQueryStatus.NotCaptured, store.Query(future, out _));
    }

    [Fact]
    public void Entity_and_global_capacity_prune_oldest_unretained_records()
    {
        var store = Create(maxRecords: 3, perEntity: 2);
        var first = Save(store, frame: 1);
        Save(store, frame: 2);
        Save(store, frame: 3);
        Assert.Equal(ContextSnapshotQueryStatus.Unavailable, store.Query(first, out _));
        Assert.Equal(2, store.HistoryCount);
        Save(store, entity: 2);
        Save(store, entity: 3);
        Assert.Equal(3, store.HistoryCount);
        Assert.Single(store.GetHistory(1, 1));
        Assert.Equal(3, store.Count);
    }

    [Fact]
    public void Retention_is_bounded_and_rejection_does_not_allocate_or_replace()
    {
        var store = Create(maxRecords: 1, perEntity: 1);
        var first = Save(store);
        var lease = store.Retain(first);
        Assert.False(store.TrySaveManaged(new ValueSnapshot(1, 2, 2), new ContextSnapshotCapture(1, 2, "captured"), out var failed));
        Assert.False(failed.IsValid);
        Assert.Throws<InvalidOperationException>(() => Save(store, entity: 2));
        Assert.Equal(0, store.PruneBeforeFrame(100));
        Assert.Equal(1, store.HistoryCount);
        lease.Dispose();
        lease.Dispose();
        var second = Save(store);
        Assert.Equal(first.SnapshotId + 1, second.SnapshotId);
        Assert.Equal(ContextSnapshotQueryStatus.Unavailable, store.Query(first, out _));
    }

    [Fact]
    public void Cleanup_updates_latest_indexes_and_invalidates_leases_without_id_reuse()
    {
        var store = Create();
        var first = Save(store, frame: 20, value: 20);
        var second = Save(store, frame: 10, value: 10);
        using var lease = store.Retain(second);
        Assert.True(store.RemoveSnapshot(second));
        Assert.Equal(20, ((ValueSnapshot)store.Get(1)).Value);
        Assert.Single(store.GetBySource(9));
        Assert.Single(store.GetByOwner(8));
        Assert.Equal(1, store.PruneBeforeFrame(21));
        Assert.Null(store.Get(1));
        Assert.Empty(store.GetBySource(9));
        Assert.Empty(store.GetByOwner(8));
        var third = Save(store);
        using var oldLease = store.Retain(third);
        store.Clear();
        var fourth = Save(store);
        using var newLease = store.Retain(fourth);
        oldLease.Dispose();
        Assert.Equal(0, store.PruneBeforeFrame(100));
        Assert.True(fourth.SnapshotId > third.SnapshotId);
        Assert.Equal(ContextSnapshotQueryStatus.Unavailable, store.Query(first, out _));
        Assert.Equal(ContextSnapshotQueryStatus.Unavailable, store.Query(third, out _));
        Assert.True(store.Remove(1));
        Assert.Equal(0, store.HistoryCount);
        Assert.False(store.Remove(1));
    }

    [Fact]
    public void Explicit_rollback_cleanup_removes_all_generations_even_when_retained()
    {
        var store = Create();
        Save(store, entity: 1);
        var removed = Save(store, entity: 2);
        Save(store, entity: 2, generation: 2);
        using var lease = store.Retain(removed);
        store.RemoveFromEntityId(2);
        Assert.Equal(1, store.HistoryCount);
        Assert.Equal(ContextSnapshotQueryStatus.Unavailable, store.Query(removed, out _));
    }

    [Fact]
    public void Managed_destroy_state_is_metadata_and_does_not_mutate_payload()
    {
        var store = Create();
        var payload = new ValueSnapshot(1, 10, 42);
        var reference = store.SaveManaged(payload, new ContextSnapshotCapture(1, 10, "removed", true));
        store.MarkDestroyed(1);
        Assert.Equal(ContextSnapshotQueryStatus.Found, store.Query(reference, out var record));
        Assert.True(record.IsDestroyed);
        Assert.Equal(42, payload.Value);
    }

    [Fact]
    public void Exact_reads_distinguish_zero_missing_field_not_captured_and_evicted()
    {
        var store = Create(maxRecords: 1, perEntity: 1);
        var first = Save(store, value: 0);
        var expectedNext = new ContextSnapshotReference(store.StorageId, first.SnapshotId + 1, first.EntityId, 2);
        var resolver = new ContextValueResolver(new ContextRegistry(), store);
        using (store.Retain(first))
        {
            Assert.False(store.TrySaveManaged(new ValueSnapshot(1, 2, 5), new ContextSnapshotCapture(2, 2, "captured"), out var rejected));
            Assert.False(rejected.IsValid);
            Assert.Equal(ContextSnapshotQueryStatus.InvalidReference, resolver.ReadSnapshot<ValueSnapshot, int>(rejected, "value").Status);
            Assert.Equal(ContextSnapshotQueryStatus.NotCaptured, resolver.ReadSnapshot<ValueSnapshot, int>(expectedNext, "value").Status);
            var zero = resolver.ReadSnapshot<ValueSnapshot, int>(first, "value");
            Assert.True(zero.Found);
            Assert.Equal(0, zero.Value);
            Assert.Equal(ContextSnapshotQueryStatus.FieldMissing, resolver.ReadSnapshot<ValueSnapshot, int>(first, "absent").Status);
        }

        var newest = Save(store, generation: 2, frame: 2, value: 5);
        Assert.Equal(expectedNext.SnapshotId, newest.SnapshotId);
        Assert.Equal(ContextSnapshotQueryStatus.Unavailable, resolver.ReadSnapshot<ValueSnapshot, int>(first, "value").Status);
        Assert.False(store.TryGetLatest<ValueSnapshot>(first.EntityId, first.Generation, "captured", out _));
        Assert.Equal(5, resolver.ReadSnapshot<ValueSnapshot, int>(newest, "value").Value);
    }

    [Fact]
    public void Exact_snapshot_read_does_not_borrow_live_fields_even_when_the_entity_is_alive()
    {
        var registry = new ContextRegistry();
        var store = Create();
        var id = registry.Create().With(new RealtimeProperty()).Build();
        Assert.True(registry.TryGetReference(id, out var identity));
        var snapshot = Save(store, entity: id, generation: identity.Generation, value: 0);
        var resolver = new ContextValueResolver(registry, store);

        Assert.True(resolver.ReadRealtime<int, RealtimeProperty>(identity, "realtimeOnly").Found);
        Assert.Equal(ContextSnapshotQueryStatus.FieldMissing, resolver.ReadSnapshot<ValueSnapshot, int>(snapshot, "realtimeOnly").Status);
        var hit = resolver.ReadSnapshot<ValueSnapshot, int>(snapshot, "value");
        Assert.True(hit.Found);
        Assert.Equal(0, hit.Value);

        Assert.True(registry.Destroy(id));
        Assert.Equal(ContextEntityQueryStatus.Unavailable, resolver.ReadRealtime<int, RealtimeProperty>(identity, "value").Status);
        Assert.True(resolver.ReadSnapshot<ValueSnapshot, int>(snapshot, "value").Found);
    }

    [Fact]
    public void Unmanaged_snapshots_keep_latest_record_semantics_but_are_not_exact_references()
    {
        var store = new SnapshotStorage();
        var newest = new LegacySnapshot(1);
        store.Save(new LegacySnapshot(1));
        store.Save(newest);
        Assert.Equal(1, store.Count);
        Assert.Equal(0, store.HistoryCount);
        Assert.Equal(2, store.GetLatestVersion(1));
        Assert.True(store.TryGetRecord(1, out var record));
        Assert.Same(newest, record.Snapshot);
        Assert.False(record.Reference.IsValid);
    }

    [Fact]
    public void Capture_metadata_and_mixed_legacy_writes_are_validated()
    {
        var store = Create();
        Assert.Throws<ArgumentException>(() => store.SaveManaged(new ValueSnapshot(1, 1, 0), default));
        Assert.Throws<ArgumentException>(() => store.SaveManaged(new ValueSnapshot(1, 1, 0), new ContextSnapshotCapture(1, 2, "captured")));
        Save(store);
        Assert.Throws<InvalidOperationException>(() => store.Save(new LegacySnapshot(1)));
        store.Remove(1);
        store.Save(new LegacySnapshot(1));
        Assert.Equal(1, store.Count);
    }

    private static SnapshotStorage Create(int maxRecords = 4096, int perEntity = 16)
    {
        var store = new SnapshotStorage(maxRecords, perEntity);
        store.RegisterType<ValueSnapshot>("test.value", 2);
        return store;
    }

    private static ContextSnapshotReference Save(SnapshotStorage store, long entity = 1, long generation = 1,
        int frame = 1, int value = 1, string kind = "captured") =>
        store.SaveManaged(new ValueSnapshot(entity, frame, value), new ContextSnapshotCapture(generation, frame, kind));

    private sealed class ValueSnapshot : IImmutableContextSnapshot, ISourceContext, IOwnerContext
    {
        public ValueSnapshot(long entity, int frame, int value) { EntityId = entity; Frame = frame; Value = value; }
        public long EntityId { get; }
        public long CreatedAtMs => 0;
        public long Version => 7;
        public int Frame { get; }
        public int Value { get; }
        public long SourceEntityId => 9;
        public long OwnerEntityId => 8;
        public bool TryGetValue<T>(string key, out T value)
        {
            if (key == "value" && Value is T typed) { value = typed; return true; }
            value = default!;
            return false;
        }
    }

    private sealed class OtherSnapshot : IImmutableContextSnapshot
    {
        public long EntityId => 1;
        public long CreatedAtMs => 0;
        public long Version => 1;
        public int Frame => 1;
        public bool TryGetValue<T>(string key, out T value) { value = default!; return false; }
    }

    private sealed class LegacySnapshot(long entityId) : IContextSnapshot
    {
        public long EntityId => entityId;
        public long CreatedAtMs => 0;
    }

    private sealed class RealtimeProperty : IProperty, IContextValueProvider
    {
        public int TypeId => PropertyTypeRegistry.Instance.Get<RealtimeProperty>()?.Id ?? 0;

        public bool TryGetValue<T>(string key, out T value)
        {
            if ((key == "value" || key == "realtimeOnly") && 11 is T typed)
            {
                value = typed;
                return true;
            }
            value = default!;
            return false;
        }
    }
}
