using AbilityKit.Ability.FrameSync;
using AbilityKit.Context;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Lifecycle;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Trace;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed class MobaBuffContextRecoveryTests : IDisposable
{
    private readonly ActorContext _actors = new();
    private readonly MobaActorRegistry _registry = new();
    private readonly MobaRuntimeContextService _contexts = new();
    private readonly MobaSkillCastRuntimeService _skills = new();
    private readonly ActorEntity _actor;
    private readonly MobaBuffStateRecoveryProvider _recovery;
    private readonly FrameIndex _frame = new(10);

    public MobaBuffContextRecoveryTests()
    {
        _actor = _actors.CreateEntity();
        _actor.AddActorId(1);
        _actor.AddBuffs(new List<BuffRuntime>());
        _registry.Register(1, _actor);
        _recovery = new MobaBuffStateRecoveryProvider(_registry, _contexts, _skills);
    }

    [Fact]
    public void Import_preserves_live_identity_version_values_and_hash()
    {
        var runtime = AddBuff();
        Bind(runtime);
        runtime.RuntimeContextVersion = 7;
        var id = runtime.RuntimeContextId;
        var payload = _recovery.ExportState(_frame);
        runtime.Remaining = 1;
        _contexts.Registry.Create().Build();

        _recovery.ImportState(_frame, payload);
        _recovery.ValidateRestoredState(_frame, payload);

        var restored = Assert.Single(_actor.buffs.Active);
        Assert.Equal(id, restored.RuntimeContextId);
        Assert.Equal(7L, restored.RuntimeContextVersion);
        Assert.Equal(8f, Read<float>(id, MobaRuntimeContextKeys.RemainingSeconds).Value);
        Assert.Equal(7L, Read<long>(id, MobaRuntimeContextKeys.Version).Value);
        Assert.True(_contexts.TryGetBuffContext(id, out var property));
        Assert.Equal(7L, property.Version);
        Assert.Equal(payload, _recovery.ExportState(_frame));
        Assert.Equal(ContextValueSource.Realtime, Read<int>(id, MobaRuntimeContextKeys.StackCount).Source);
        Assert.False(_contexts.Snapshots.TryGetRecord(id, out _));
        Assert.NotEqual(id, Bind(AddBuff(2)));
    }

    [Theory]
    [InlineData(0L, 0L)]
    [InlineData(5L, 3L)]
    [InlineData(100L, 2L)]
    public void Import_restores_persisted_or_legacy_identity_without_cursor_collision(long id, long version)
    {
        var runtime = AddBuff();
        runtime.RuntimeContextId = id;
        runtime.RuntimeContextVersion = version;
        var payload = _recovery.ExportState(_frame);
        _actor.buffs.Active.Clear();
        for (var i = 0; i < 10; i++) _contexts.Registry.Create().Build();
        if (id == 5) _contexts.Registry.Destroy(id);

        _recovery.ImportState(_frame, payload);
        _recovery.ValidateRestoredState(_frame, payload);

        var restored = Assert.Single(_actor.buffs.Active);
        Assert.Equal(id, restored.RuntimeContextId);
        Assert.Equal(version, restored.RuntimeContextVersion);
        if (id != 0) Assert.True(_contexts.Registry.Exists(id));
        Assert.True(_contexts.Registry.Create().Build() > Math.Max(10, id));
    }

    [Theory]
    [InlineData(0L, 1L)]
    [InlineData(20L, 0L)]
    [InlineData(-1L, 1L)]
    [InlineData(long.MaxValue, 1L)]
    public void Invalid_reference_is_rejected_before_existing_buff_is_cleared(long id, long version)
    {
        var existing = AddBuff();
        Bind(existing);
        var incoming = NewBuff(2);
        incoming.RuntimeContextId = id;
        incoming.RuntimeContextVersion = version;

        Assert.Throws<InvalidOperationException>(() => _recovery.ImportState(_frame, Payload(incoming)));
        Assert.Same(existing, Assert.Single(_actor.buffs.Active));
        Assert.True(_contexts.Registry.Exists(existing.RuntimeContextId));
    }

    [Fact]
    public void Duplicate_and_unrelated_contexts_are_rejected_before_cleanup()
    {
        var existing = AddBuff();
        var id = Bind(existing);
        var duplicate = NewBuff(2);
        duplicate.RuntimeContextId = id;
        duplicate.RuntimeContextVersion = 1;
        Assert.Throws<InvalidOperationException>(() => _recovery.ImportState(_frame, Payload(existing, duplicate)));

        duplicate.RuntimeContextId = _contexts.Registry.Create().Build();
        Assert.Throws<InvalidOperationException>(() => _recovery.ImportState(_frame, Payload(duplicate)));
        Assert.Same(existing, Assert.Single(_actor.buffs.Active));
        Assert.True(_contexts.Registry.Exists(id));
    }

    [Fact]
    public void Stale_runtime_reference_cannot_claim_or_destroy_another_context()
    {
        var existing = AddBuff();
        var unrelatedId = _contexts.Registry.Create().Build();
        existing.RuntimeContextId = unrelatedId;
        existing.RuntimeContextVersion = 1;
        Assert.Throws<InvalidOperationException>(() => _recovery.ImportState(_frame, Payload(existing)));
        Assert.Throws<InvalidOperationException>(() => Bind(existing));
        Assert.Throws<InvalidOperationException>(() => _contexts.SnapshotAndDestroyBuffContext(existing, MobaRuntimeContextLifecycleState.Ended, 10));
        Assert.True(_contexts.Registry.Exists(unrelatedId));
        Assert.Same(existing, Assert.Single(_actor.buffs.Active));
        existing.RuntimeContextId = 0;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void End_keeps_frozen_reference_for_callbacks_then_clears_even_when_callback_throws(bool withoutTrace)
    {
        var runtime = AddBuff();
        if (withoutTrace) runtime.SourceContextId = 0;
        var id = Bind(runtime);
        runtime.RuntimeContextVersion = 4;
        var hook = new CallbackHook(() =>
        {
            Assert.Equal(id, runtime.RuntimeContextId);
            Assert.Equal(4L, runtime.RuntimeContextVersion);
            Assert.False(_contexts.Registry.Exists(id));
            runtime.StackCount = 99;
            var frozen = Read<int>(id, MobaRuntimeContextKeys.StackCount);
            Assert.Equal(ContextValueSource.Snapshot, frozen.Source);
            Assert.Equal(2, frozen.Value);
            Assert.Equal(4L, Read<long>(id, MobaRuntimeContextKeys.Version).Value);
            _contexts.SnapshotAndDestroyBuffContext(runtime, MobaRuntimeContextLifecycleState.Destroyed, 99, true);
            Assert.Equal(MobaRuntimeContextLifecycleState.Ended, Read<MobaRuntimeContextLifecycleState>(id, MobaRuntimeContextKeys.LifecycleState).Value);
            throw new InvalidOperationException("callback failed");
        });
        var hooks = new MobaRuntimeLifecycleHookService();
        hooks.Register(hook);
        var flow = new BuffEndFlow(null, new BuffContextRegistry(null, _contexts, null), null,
            new BuffRuntimeBindingCoordinator(hooks, null, null));

        var error = Assert.Throws<InvalidOperationException>(() => flow.EndRuntime(_actor, _actor.buffs.Active, 0, runtime, 2, TraceLifecycleReason.Expired));
        Assert.Equal("callback failed", error.Message);
        Assert.True(hook.Called);
        Assert.Empty(_actor.buffs.Active);
        Assert.Equal(0L, runtime.RuntimeContextId);
        Assert.Equal(0L, runtime.RuntimeContextVersion);
        Assert.Equal(2, Read<int>(id, MobaRuntimeContextKeys.StackCount).Value);
    }

    [Fact]
    public void Cancel_without_trace_still_destroys_runtime_context_and_clears_reference()
    {
        var runtime = AddBuff();
        runtime.SourceContextId = 0;
        var id = Bind(runtime);
        new BuffContextRegistry(null, _contexts, null).CancelAndEnd(runtime);
        Assert.False(_contexts.Registry.Exists(id));
        Assert.Equal(0L, runtime.RuntimeContextId);
        Assert.Equal(0L, runtime.RuntimeContextVersion);
        Assert.Equal(ContextValueSource.Snapshot, Read<int>(id, MobaRuntimeContextKeys.StackCount).Source);
    }

    [Fact]
    public void Final_snapshot_has_explicit_metadata_and_remains_detached_from_runtime()
    {
        var runtime = AddBuff();
        var id = Bind(runtime);
        _contexts.SnapshotAndDestroyBuffContext(runtime, MobaRuntimeContextLifecycleState.Ended, 20, true);
        Assert.True(_contexts.SnapshotReader.TryGetRecord(id, out var record));
        Assert.True(record.IsManaged);
        Assert.True(record.IsDestroyed);
        Assert.Equal("moba.buff.state", record.TypeId);
        Assert.Equal("removed", record.Kind);
        Assert.Equal(1, record.SchemaVersion);
        Assert.Equal(20, record.Frame);
        runtime.StackCount = 99;
        runtime.Remaining = 99;
        var result = _contexts.Resolver.ReadSnapshot<MobaBuffContextSnapshot, int>(record.Reference, MobaRuntimeContextKeys.StackCount);
        Assert.Equal(2, result.Value);
        Assert.False(((MobaBuffContextSnapshot)record.Snapshot).IsRealtimeAvailable);
        Assert.Equal(ContextSnapshotQueryStatus.FieldMissing,
            _contexts.SnapshotReader.ReadSnapshot<MobaBuffContextSnapshot, int>(record.Reference, "absent").Status);
    }

    [Fact]
    public void Restored_context_gets_new_snapshot_generation_without_changing_authoritative_version()
    {
        var runtime = AddBuff();
        var id = Bind(runtime);
        runtime.RuntimeContextVersion = 7;
        _contexts.SnapshotAndDestroyBuffContext(runtime, MobaRuntimeContextLifecycleState.Ended, 20, true);
        Assert.True(_contexts.SnapshotReader.TryGetRecord(id, out var before));
        _contexts.RestoreBuffContext(runtime, MobaBuffRuntimeContextData.FromRuntime(runtime, 1, 10, MobaRuntimeContextLifecycleState.Active));
        Assert.Equal(ContextSnapshotQueryStatus.Unavailable, _contexts.SnapshotReader.Query(before.Reference, out _));
        _contexts.SnapshotAndDestroyBuffContext(runtime, MobaRuntimeContextLifecycleState.Ended, 21, true);
        Assert.True(_contexts.SnapshotReader.TryGetRecord(id, out var after));
        Assert.True(after.Reference.Generation > before.Reference.Generation);
        Assert.True(after.Reference.SnapshotId > before.Reference.SnapshotId);
        Assert.Equal(7, runtime.RuntimeContextVersion);
        Assert.Equal(7, after.Version);
    }

    [Fact]
    public void Retained_history_does_not_prevent_buff_cleanup_or_world_cleanup()
    {
        var runtime = AddBuff();
        var id = Bind(runtime);
        _contexts.SnapshotAndDestroyBuffContext(runtime, MobaRuntimeContextLifecycleState.Ended, 20, true);
        Assert.True(_contexts.SnapshotReader.TryGetRecord(id, out var record));
        using var lease = _contexts.Snapshots.Retain(record.Reference);
        _contexts.SnapshotAndDestroyBuffContext(runtime, MobaRuntimeContextLifecycleState.Destroyed, 21);
        Assert.Equal(0, runtime.RuntimeContextId);
        Assert.False(_contexts.Registry.Exists(id));
        _contexts.Dispose();
        Assert.Equal(ContextSnapshotQueryStatus.Unavailable, _contexts.SnapshotReader.Query(record.Reference, out _));
        Assert.Equal(0, _contexts.SnapshotReader.HistoryCount);
    }

    private BuffRuntime AddBuff(int buffId = 1)
    {
        var runtime = NewBuff(buffId);
        _actor.buffs.Active.Add(runtime);
        return runtime;
    }

    private static BuffRuntime NewBuff(int buffId) => new()
    {
        BuffId = buffId, SourceId = 2, SourceContextId = 1000 + buffId,
        Remaining = 8f, IntervalRemainingSeconds = 1f, StackCount = 2,
    };

    private long Bind(BuffRuntime runtime) => _contexts.EnsureBuffContext(runtime,
        MobaBuffRuntimeContextData.FromRuntime(runtime, 1, 10, MobaRuntimeContextLifecycleState.Active));

    private ContextValueResult<T> Read<T>(long id, string key) => _contexts.Resolver.GetValue<T, MobaBuffContextProperty>(id, key);

    private static byte[] Payload(params BuffRuntime[] runtimes) => MemoryPackSerializer.Serialize(
        new MobaBuffStateRecoveryPayload(MobaBuffStateRecoveryProvider.CurrentPayloadVersion,
            runtimes.Select(runtime => MobaBuffStateRecoveryEntry.FromRuntime(1, runtime)).ToArray()));

    public void Dispose()
    {
        foreach (var runtime in _actor.buffs.Active)
            _contexts.SnapshotAndDestroyBuffContext(runtime, MobaRuntimeContextLifecycleState.Destroyed, 10);
        _skills.Dispose();
        _contexts.Dispose();
        _registry.Dispose();
        _actors.DestroyAllEntities();
    }

    private sealed class CallbackHook(Action callback) : IMobaRuntimeLifecycleHook
    {
        public bool Called { get; private set; }
        public void OnRuntimeLifecycle(in MobaRuntimeLifecycleEvent evt)
        {
            if (evt.Kind != MobaRuntimeLifecycleEventKind.Ended) return;
            Called = true;
            callback();
        }
    }
}
