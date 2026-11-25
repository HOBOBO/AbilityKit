using AbilityKit.Context;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Events.Buff;
using AbilityKit.Demo.Moba.Services;
using MemoryPack;
using Xunit;
using AbilityKit.Demo.Moba.Services.Buffs;

namespace AbilityKit.Demo.Moba.Tests.Trace;

public sealed class MobaRuntimeContextIdentityTests
{
    [Fact]
    public void Bound_reference_reads_live_changes_and_keeps_business_version_separate()
    {
        using var contexts = new MobaRuntimeContextService();
        var runtime = Bind(contexts);
        Assert.True(contexts.TryGetBuffReference(runtime, out var reference));
        runtime.StackCount = 0;
        var result = Read<int>(reference, contexts, MobaRuntimeContextKeys.StackCount);
        Assert.True(result.Found);
        Assert.True(result.IsVersionChecked);
        Assert.Equal(ContextValueSource.Realtime, result.Source);
        Assert.Equal(0, result.Value);
        Assert.False(result.ResolvedSnapshot.IsValid);
        runtime.RuntimeContextVersion++;
        Assert.Equal(MobaRuntimeContextValueFailure.VersionMismatch, Read<int>(reference, contexts, MobaRuntimeContextKeys.StackCount).Failure);
    }

    [Fact]
    public void Cross_service_same_id_and_version_cannot_match()
    {
        using var first = new MobaRuntimeContextService();
        using var second = new MobaRuntimeContextService();
        var runtime = Bind(first);
        var other = Bind(second);
        Assert.Equal(runtime.RuntimeContextId, other.RuntimeContextId);
        Assert.Equal(runtime.RuntimeContextVersion, other.RuntimeContextVersion);
        first.TryGetBuffReference(runtime, out var reference);
        Assert.Equal(MobaRuntimeContextValueFailure.IdentityMismatch, Read<int>(reference, second, MobaRuntimeContextKeys.StackCount).Failure);
    }

    [Fact]
    public void Restored_same_id_and_version_rejects_old_reference()
    {
        using var contexts = new MobaRuntimeContextService();
        var runtime = Bind(contexts);
        contexts.TryGetBuffReference(runtime, out var old);
        contexts.SnapshotAndDestroyBuffContext(runtime, MobaRuntimeContextLifecycleState.Ended, 20, true);
        var before = MobaBuffStateRecoveryEntry.FromRuntime(1, runtime);
        var payload = MemoryPackSerializer.Serialize(new MobaBuffStateRecoveryPayload(
            MobaBuffStateRecoveryProvider.CurrentPayloadVersion, new[] { before }));
        contexts.RestoreBuffContext(runtime, Data(runtime));
        contexts.TryGetBuffReference(runtime, out var restored);
        Assert.True(restored.Identity.Generation > old.Identity.Generation);
        Assert.Equal(old.Version, restored.Version);
        Assert.Equal(MobaRuntimeContextValueFailure.IdentityMismatch, Read<int>(old, contexts, MobaRuntimeContextKeys.StackCount).Failure);
        Assert.Equal(MobaRuntimeContextValueFailure.IdentityMismatch, ReadFinal<int>(old, contexts, MobaRuntimeContextKeys.StackCount).Failure);
        Assert.True(Read<int>(restored, contexts, MobaRuntimeContextKeys.StackCount).Found);
        Assert.Equal(payload, MemoryPackSerializer.Serialize(new MobaBuffStateRecoveryPayload(
            MobaBuffStateRecoveryProvider.CurrentPayloadVersion, new[] { MobaBuffStateRecoveryEntry.FromRuntime(1, runtime) })));
    }

    [Fact]
    public void Removed_reference_resolves_its_generation_snapshot_and_returns_exact_pin()
    {
        using var contexts = new MobaRuntimeContextService();
        var runtime = Bind(contexts);
        contexts.TryGetBuffReference(runtime, out var reference);
        contexts.SnapshotAndDestroyBuffContext(runtime, MobaRuntimeContextLifecycleState.Ended, 20, true);
        runtime.StackCount = 99;
        var result = ReadFinal<int>(reference, contexts, MobaRuntimeContextKeys.StackCount);
        Assert.True(result.Found);
        Assert.Equal(ContextValueSource.Snapshot, result.Source);
        Assert.Equal(2, result.Value);
        Assert.Equal(reference.Identity.Generation, result.ResolvedSnapshot.Generation);
        Assert.True(contexts.SnapshotReader.ReadSnapshot<MobaBuffContextSnapshot, int>(result.ResolvedSnapshot, MobaRuntimeContextKeys.StackCount).Found);
        Assert.Equal(MobaRuntimeContextValueFailure.ContextUnavailable,
            Read<int>(reference, contexts, MobaRuntimeContextKeys.StackCount).Failure);
        contexts.Snapshots.Remove(reference.ContextId);
        Assert.Equal(MobaRuntimeContextValueFailure.SnapshotUnavailable, ReadFinal<int>(reference, contexts, MobaRuntimeContextKeys.StackCount).Failure);
        Assert.Equal(ContextSnapshotQueryStatus.Unavailable, contexts.SnapshotReader.Query(result.ResolvedSnapshot, out _));
    }

    [Fact]
    public void Final_snapshot_distinguishes_a_zero_value_missing_field_and_eviction()
    {
        using var contexts = new MobaRuntimeContextService();
        var runtime = Bind(contexts);
        Assert.True(contexts.TryGetBuffReference(runtime, out var reference));
        runtime.StackCount = 0;
        contexts.SnapshotAndDestroyBuffContext(runtime, MobaRuntimeContextLifecycleState.Ended, 20, true);

        var zero = ReadFinal<int>(reference, contexts, MobaRuntimeContextKeys.StackCount);
        Assert.True(zero.Found);
        Assert.Equal(ContextValueSource.Snapshot, zero.Source);
        Assert.Equal(0, zero.Value);
        var missing = ReadFinal<int>(reference, contexts, "unknown-field");
        Assert.False(missing.Found);
        Assert.Equal(MobaRuntimeContextValueFailure.ValueMissing, missing.Failure);

        Assert.Equal(1, contexts.Snapshots.PruneBeforeFrame(21));
        Assert.Equal(ContextSnapshotQueryStatus.Unavailable, contexts.SnapshotReader.Query(zero.ResolvedSnapshot, out _));
        var evicted = ReadFinal<int>(reference, contexts, MobaRuntimeContextKeys.StackCount);
        Assert.False(evicted.Found);
        Assert.Equal(MobaRuntimeContextValueFailure.SnapshotUnavailable, evicted.Failure);
    }

    [Fact]
    public void Rollback_reused_prediction_id_does_not_revalidate_old_reference()
    {
        using var contexts = new MobaRuntimeContextService();
        var confirmed = Bind(contexts);
        contexts.TryGetBuffReference(confirmed, out var confirmedRef);
        var cursor = contexts.Registry.NextEntityId;
        var predicted = Bind(contexts);
        contexts.TryGetBuffReference(predicted, out var predictedRef);
        contexts.RestoreRollbackEntityCursor(cursor, new[] { confirmed.RuntimeContextId });
        var replay = Bind(contexts);
        Assert.Equal(predictedRef.ContextId, replay.RuntimeContextId);
        Assert.Equal(MobaRuntimeContextValueFailure.IdentityMismatch, Read<int>(predictedRef, contexts, MobaRuntimeContextKeys.StackCount).Failure);
        Assert.True(Read<int>(confirmedRef, contexts, MobaRuntimeContextKeys.StackCount).Found);
    }

    [Fact]
    public void Raw_registry_clear_prunes_provider_and_invalidates_old_reference()
    {
        using var contexts = new MobaRuntimeContextService();
        var runtime = Bind(contexts);
        contexts.TryGetBuffReference(runtime, out var old);
        contexts.Registry.Clear();
        Assert.False(contexts.TryGetBuffReference(runtime, out _));
        var replay = Bind(contexts);
        Assert.Equal(old.ContextId, replay.RuntimeContextId);
        Assert.Equal(MobaRuntimeContextValueFailure.IdentityMismatch, Read<int>(old, contexts, MobaRuntimeContextKeys.StackCount).Failure);
        Assert.Throws<InvalidOperationException>(() => contexts.EnsureBuffContext(runtime, Data(runtime)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Invalid_rollback_does_not_unbind_or_delete_snapshots(long cursor)
    {
        using var contexts = new MobaRuntimeContextService();
        var runtime = Bind(contexts);
        contexts.TryGetBuffReference(runtime, out var reference);
        var predicted = Bind(contexts);
        var removed = Bind(contexts);
        var removedId = removed.RuntimeContextId;
        contexts.SnapshotAndDestroyBuffContext(removed, MobaRuntimeContextLifecycleState.Ended, 20, true);
        Assert.True(contexts.SnapshotReader.TryGetRecord(removedId, out var record));
        Assert.ThrowsAny<Exception>(() => contexts.RestoreRollbackEntityCursor(cursor, new[] { runtime.RuntimeContextId }));
        Assert.True(Read<int>(reference, contexts, MobaRuntimeContextKeys.StackCount).Found);
        Assert.True(contexts.TryGetBuffReference(predicted, out _));
        Assert.Equal(ContextSnapshotQueryStatus.Found, contexts.SnapshotReader.Query(record.Reference, out _));
    }

    [Fact]
    public void Buff_payloads_without_frozen_identity_cannot_read_from_a_live_runtime()
    {
        using var contexts = new MobaRuntimeContextService();
        var runtime = Bind(contexts);
        var stage = new BuffTriggerContext
        {
            Runtime = runtime, RuntimeContextId = runtime.RuntimeContextId, RuntimeContextVersion = runtime.RuntimeContextVersion
        };
        var evt = new BuffEventArgs
        {
            Runtime = runtime, RuntimeContextId = runtime.RuntimeContextId, RuntimeContextVersion = runtime.RuntimeContextVersion
        };
        Assert.False(stage.TryGetRuntimeContext(out _));
        Assert.False(evt.TryGetRuntimeContext(out _));
        Assert.Equal(MobaRuntimeContextValueFailure.MissingRuntimeContext,
            ((object)stage).ReadRealtimeContextValue<int, MobaBuffContextProperty>(contexts, MobaRuntimeContextKeys.StackCount).Failure);
        Assert.Equal(MobaRuntimeContextValueFailure.MissingRuntimeContext,
            ((object)evt).ReadRealtimeContextValue<int, MobaBuffContextProperty>(contexts, MobaRuntimeContextKeys.StackCount).Failure);
    }

    [Fact]
    public void Realtime_and_final_reads_do_not_substitute_for_each_other()
    {
        using var contexts = new MobaRuntimeContextService();
        var runtime = Bind(contexts);
        Assert.True(contexts.TryGetBuffReference(runtime, out var older));
        Assert.Equal(MobaRuntimeContextValueFailure.SnapshotUnavailable, ReadFinal<int>(older, contexts, MobaRuntimeContextKeys.StackCount).Failure);
        runtime.RuntimeContextVersion++;
        Assert.True(contexts.TryGetBuffReference(runtime, out var newer));

        Assert.Equal(MobaRuntimeContextValueFailure.VersionMismatch, Read<int>(older, contexts, MobaRuntimeContextKeys.StackCount).Failure);
        runtime.StackCount = 3;
        Assert.Equal(3, Read<int>(newer, contexts, MobaRuntimeContextKeys.StackCount).Value);
        contexts.SnapshotAndDestroyBuffContext(runtime, MobaRuntimeContextLifecycleState.Ended, 20, true);
        Assert.Equal(MobaRuntimeContextValueFailure.ContextUnavailable, Read<int>(newer, contexts, MobaRuntimeContextKeys.StackCount).Failure);
        Assert.Equal(3, ReadFinal<int>(newer, contexts, MobaRuntimeContextKeys.StackCount).Value);
        Assert.Equal(MobaRuntimeContextValueFailure.VersionMismatch, ReadFinal<int>(older, contexts, MobaRuntimeContextKeys.StackCount).Failure);
    }

    [Fact]
    public void One_resolved_property_is_used_even_if_registry_property_is_replaced_during_version_read()
    {
        using var contexts = new MobaRuntimeContextService();
        var property = new TestProperty(true);
        var id = contexts.Registry.Create().With(property).Build();
        property.OnVersionRead = () => contexts.Registry.Set(id, new TestProperty(false));
        Assert.True(contexts.Registry.TryGetReference(id, out var identity));
        object reference = new MobaRuntimeContextReference(identity, 1);
        var result = reference.ReadRealtimeContextValue<int, TestProperty>(contexts, "value");
        Assert.True(result.Found);
        Assert.Equal(42, result.Value);
        Assert.Equal(ContextValueSource.Realtime, result.Source);
    }

    [Fact]
    public void Stage_and_event_payloads_freeze_identity_instead_of_following_rebind()
    {
        using var contexts = new MobaRuntimeContextService();
        var runtime = Bind(contexts);
        var stage = new BuffTriggerContext
        {
            Runtime = runtime, RuntimeContextId = runtime.RuntimeContextId,
            RuntimeContextVersion = runtime.RuntimeContextVersion, RuntimeContextIdentity = runtime.RuntimeContextIdentity
        };
        var evt = new BuffEventArgs
        {
            Runtime = runtime, RuntimeContextId = runtime.RuntimeContextId,
            RuntimeContextVersion = runtime.RuntimeContextVersion, RuntimeContextIdentity = runtime.RuntimeContextIdentity
        };
        Assert.True(stage.TryGetRuntimeContext(out var before));
        contexts.SnapshotAndDestroyBuffContext(runtime, MobaRuntimeContextLifecycleState.Ended, 20, true);
        contexts.RestoreBuffContext(runtime, Data(runtime));
        Assert.True(stage.TryGetRuntimeContext(out var after));
        Assert.Equal(before.Identity.Generation, after.Identity.Generation);
        Assert.Equal(MobaRuntimeContextValueFailure.IdentityMismatch, ((object)stage).ReadRealtimeContextValue<int, MobaBuffContextProperty>(contexts, MobaRuntimeContextKeys.StackCount).Failure);
        Assert.Equal(MobaRuntimeContextValueFailure.IdentityMismatch, ((object)evt).ReadRealtimeContextValue<int, MobaBuffContextProperty>(contexts, MobaRuntimeContextKeys.StackCount).Failure);
    }

    [Theory]
    [InlineData(0, "captured")]
    [InlineData(1, "removed")]
    public void Final_buff_lookup_rejects_latest_record_with_wrong_generation_or_kind(long generationOffset, string kind)
    {
        using var contexts = new MobaRuntimeContextService();
        var runtime = Bind(contexts);
        Assert.True(contexts.TryGetBuffReference(runtime, out var reference));
        contexts.SnapshotAndDestroyBuffContext(runtime, MobaRuntimeContextLifecycleState.Ended, 20, true);
        contexts.Snapshots.Remove(reference.ContextId);
        var unrelated = MobaBuffContextSnapshot.FromRuntime(runtime, 1, 21, MobaRuntimeContextLifecycleState.Ended);
        contexts.Snapshots.SaveManaged(unrelated, new ContextSnapshotCapture(reference.Identity.Generation + generationOffset, 21, kind));
        Assert.Equal(MobaRuntimeContextValueFailure.SnapshotUnavailable,
            ReadFinal<int>(reference, contexts, MobaRuntimeContextKeys.StackCount).Failure);
    }

    [Fact]
    public void Disposal_during_read_cannot_return_success_from_captured_property()
    {
        using var contexts = new MobaRuntimeContextService();
        var property = new TestProperty(true) { OnVersionRead = contexts.Dispose };
        var id = contexts.Registry.Create().With(property).Build();
        Assert.True(contexts.Registry.TryGetReference(id, out var identity));
        object reference = new MobaRuntimeContextReference(identity, 1);
        var result = reference.ReadRealtimeContextValue<int, TestProperty>(contexts, "value");
        Assert.False(result.Found);
        Assert.Equal(MobaRuntimeContextValueFailure.ContextServiceDisposed, result.Failure);
    }

    [Fact]
    public void Incomplete_identity_or_version_is_rejected_and_dispose_cannot_rebind()
    {
        using var contexts = new MobaRuntimeContextService();
        var runtime = Bind(contexts);
        contexts.TryGetBuffReference(runtime, out var reference);
        object invalid = new MobaRuntimeContextReference(
            new ContextEntityReference(contexts.Registry.RegistryId, runtime.RuntimeContextId, 0), 1);
        Assert.Equal(MobaRuntimeContextValueFailure.InvalidRuntimeContext,
            invalid.ReadRealtimeContextValue<int, MobaBuffContextProperty>(contexts, MobaRuntimeContextKeys.StackCount).Failure);
        Assert.Equal(MobaRuntimeContextValueFailure.InvalidRuntimeContext,
            Read<int>(new MobaRuntimeContextReference(reference.Identity, 0), contexts, MobaRuntimeContextKeys.StackCount).Failure);
        contexts.Dispose();
        contexts.Dispose();
        Assert.Equal(MobaRuntimeContextValueFailure.ContextServiceDisposed, Read<int>(reference, contexts, MobaRuntimeContextKeys.StackCount).Failure);
        Assert.False(contexts.TryGetBuffReference(runtime, out _));
        Assert.Throws<ObjectDisposedException>(() => contexts.EnsureBuffContext(runtime, Data(runtime)));
        Assert.Throws<ObjectDisposedException>(() => contexts.RestoreBuffContext(runtime, Data(runtime)));
        Assert.Throws<ObjectDisposedException>(() => contexts.RestoreRollbackEntityCursor(1, Array.Empty<long>()));
    }

    private static BuffRuntime Bind(MobaRuntimeContextService contexts)
    {
        var runtime = new BuffRuntime { BuffId = 1, StackCount = 2, Remaining = 8, SourceId = 2 };
        contexts.EnsureBuffContext(runtime, Data(runtime));
        return runtime;
    }

    private static MobaBuffRuntimeContextData Data(BuffRuntime runtime) =>
        MobaBuffRuntimeContextData.FromRuntime(runtime, 1, 10, MobaRuntimeContextLifecycleState.Active);

    private static MobaRuntimeContextValueResult<T> Read<T>(MobaRuntimeContextReference reference,
        MobaRuntimeContextService contexts, string key) => reference.ReadRealtimeContextValue<T, MobaBuffContextProperty>(contexts, key);

    private static MobaRuntimeContextValueResult<T> ReadFinal<T>(MobaRuntimeContextReference reference,
        MobaRuntimeContextService contexts, string key) => reference.ReadFinalBuffContextValue<T>(contexts, key);

    private sealed class TestProperty(bool hasValue) : IProperty, IContextValueProvider
    {
        public Action? OnVersionRead;
        public int TypeId => PropertyTypeRegistry.Instance.Get<TestProperty>()?.Id ?? 0;
        public bool TryGetValue<T>(string key, out T value)
        {
            if (key == MobaRuntimeContextKeys.Version)
            {
                OnVersionRead?.Invoke();
                if (1L is T version) { value = version; return true; }
            }
            if (key == "value" && hasValue && 42 is T typed) { value = typed; return true; }
            value = default!;
            return false;
        }
    }

}
