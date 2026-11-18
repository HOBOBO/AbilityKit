using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.Share.Impl.Pipeline.Skill;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Pipeline;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed class MobaCastRetentionRollbackTests
{
    [Theory]
    [InlineData(SkillCastStage.Completed, SkillDestroyReason.CompletedRetainExpired, 0)]
    [InlineData(SkillCastStage.Completed, SkillDestroyReason.CompletedRetainExpired, 10)]
    [InlineData(SkillCastStage.Failed, SkillDestroyReason.FailedRetainExpired, 0)]
    [InlineData(SkillCastStage.Failed, SkillDestroyReason.FailedRetainExpired, 10)]
    [InlineData(SkillCastStage.Cancelled, SkillDestroyReason.CancelledRetainExpired, 0)]
    [InlineData(SkillCastStage.Cancelled, SkillDestroyReason.CancelledRetainExpired, 10)]
    public void Terminal_deadline_and_reason_stay_fixed_until_confirmation(SkillCastStage stage, SkillDestroyReason reason, int gate)
    {
        using var f = new Fixture(gate: gate);
        var cast = f.Cast(stage);
        f.Tick(130);
        Assert.False(cast.hasSkillCastDestroyRequest);
        f.Tick(131);
        AssertRequest(cast, 131, 131 + gate, reason);
        var payload = f.Provider.Export(new FrameIndex(131));
        // Prediction keeps moving while confirmation stalls. No deadline may move.
        f.Tick(180);
        Assert.True(cast.isEnabled);
        AssertRequest(cast, 131, 131 + gate, reason);
        Assert.Equal(payload, f.Provider.Export(new FrameIndex(180)));
        f.Frames.Confirmed = 130 + gate;
        f.Cleanup.Execute();
        Assert.True(cast.isEnabled);
        f.Frames.Confirmed++;
        f.Cleanup.Execute();
        Assert.False(cast.isEnabled);
        f.Cleanup.Execute();
        Assert.Empty(f.Contexts.actor.GetEntities());
    }

    [Fact]
    public void Jump_ahead_uses_anchor_deadline_and_zero_retention_disables_requests()
    {
        using var f = new Fixture();
        var cast = f.Cast();
        f.Tick(400);
        AssertRequest(cast, 131, 141, SkillDestroyReason.CompletedRetainExpired);
        using var disabled = new Fixture(retain: 0);
        var retained = disabled.Cast();
        disabled.Tick(400);
        Assert.False(retained.hasSkillCastDestroyRequest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Terminal_or_pending_checkpoint_restores_anchor_and_rebuilds_fresh_index(bool pending)
    {
        using var f = new Fixture();
        var cast = f.Cast(SkillCastStage.Cancelled);
        var frame = new FrameIndex(pending ? 135 : 120);
        f.Tick(frame.Value);
        var baseline = f.Provider.Export(frame);
        f.Tick(190);
        cast.ReplaceSkillCastRetention(180); // future cache/state must not survive restore
        f.Provider.Import(frame, baseline);
        Assert.Equal(baseline, f.Provider.Export(frame));
        var fresh = f.NewSync();
        f.Frames.Predicted = pending ? 160 : 130;
        fresh.Execute();
        Assert.Equal(pending, cast.hasSkillCastDestroyRequest);
        f.Frames.Predicted = 170;
        fresh.Execute();
        AssertRequest(cast, 131, 141, SkillDestroyReason.CancelledRetainExpired);
        f.Frames.Confirmed = 141;
        f.Cleanup.Execute();
        Assert.False(cast.isEnabled);
    }

    [Fact]
    public void Real_runner_projection_clears_stale_request_and_restores_running_checkpoint()
    {
        using var f = new Fixture();
        f.Contexts.actor.CreateEntity().AddActorId(1);
        var runner = f.StartRunner();
        f.Tick(100);
        var cast = Assert.Single(f.Contexts.actor.GetGroup(ActorMatcher.SkillCastInstanceId).GetEntities());
        Assert.True(cast.isSkillCastRunningTag);
        Assert.Equal(100, cast.skillCastRetention.LastObservedFrame);
        var checkpoint = f.Provider.Export(new FrameIndex(100));
        cast.AddSkillCastDestroyRequest(80, 90, SkillDestroyReason.CompletedRetainExpired);
        cast.AddSkillCastCancelRequest(90, SkillCancelReason.Interrupted);
        f.Tick(105);
        Assert.False(cast.hasSkillCastDestroyRequest);
        Assert.False(cast.hasSkillCastCancelRequest);
        Assert.Equal(105, cast.skillCastRetention.LastObservedFrame);
        cast.AddSkillCastDestroyRequest(140, 150, SkillDestroyReason.CompletedRetainExpired);
        f.Provider.Import(new FrameIndex(100), checkpoint);
        Assert.Equal(100, cast.skillCastRetention.LastObservedFrame);
        Assert.False(cast.hasSkillCastDestroyRequest);
        f.Tick(110);
        Assert.True(cast.isSkillCastRunningTag);
        Assert.False(cast.hasSkillCastDestroyRequest);
        Assert.Equal(110, cast.skillCastRetention.LastObservedFrame);
        runner.Step(2f);
        f.Tick(111);
        Assert.False(cast.isSkillCastRunningTag);
        Assert.Equal(111, cast.skillCastRetention.LastObservedFrame);
        f.Tick(141);
        Assert.False(cast.hasSkillCastDestroyRequest);
        f.Tick(142);
        AssertRequest(cast, 142, 152, SkillDestroyReason.CompletedRetainExpired);
    }

    [Fact]
    public void Pooled_entity_with_new_identity_cannot_inherit_old_cache_deadline()
    {
        using var f = new Fixture();
        var original = f.Cast();
        f.Tick(120);
        original.Destroy();
        var replacement = f.Cast(last: 120, id: 8);
        Assert.Same(original, replacement);
        f.Tick(131);
        Assert.False(replacement.hasSkillCastDestroyRequest);
        f.Tick(151);
        AssertRequest(replacement, 151, 161, SkillDestroyReason.CompletedRetainExpired);
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("missing-anchor")]
    [InlineData("negative-anchor")]
    [InlineData("future-anchor")]
    [InlineData("request-before-anchor")]
    [InlineData("future-request")]
    [InlineData("confirmation-before-request")]
    [InlineData("running-request")]
    [InlineData("reason")]
    [InlineData("missing-trailer")]
    [InlineData("bad-trailer")]
    [InlineData("truncated")]
    [InlineData("actor-with-cast-state")]
    [InlineData("duplicate")]
    [InlineData("dual-identity")]
    public void Invalid_component_snapshot_rejects_before_all_imports_and_keeps_live_bytes(string defect)
    {
        using var f = new Fixture();
        var actor = f.Contexts.actor.CreateEntity();
        actor.AddActorId(1);
        actor.AddLifetime(5);
        var cast = f.Cast();
        cast.AddSkillCastDestroyRequest(131, 141, SkillDestroyReason.CompletedRetainExpired);
        var frame = new FrameIndex(140);
        switch (defect)
        {
            case "missing-anchor": cast.RemoveSkillCastRetention(); break;
            case "negative-anchor": cast.ReplaceSkillCastRetention(-1); break;
            case "future-anchor": cast.ReplaceSkillCastRetention(141); break;
            case "request-before-anchor": cast.ReplaceSkillCastDestroyRequest(100, 141, SkillDestroyReason.CompletedRetainExpired); break;
            case "future-request": cast.ReplaceSkillCastDestroyRequest(141, 151, SkillDestroyReason.CompletedRetainExpired); break;
            case "confirmation-before-request": cast.ReplaceSkillCastDestroyRequest(131, 130, SkillDestroyReason.CompletedRetainExpired); break;
            case "running-request": cast.isSkillCastRunningTag = true; break;
            case "reason": cast.ReplaceSkillCastDestroyRequest(131, 141, (SkillDestroyReason)999); break;
            case "actor-with-cast-state": actor.AddSkillCastRetention(100); break;
        }
        var bad = MemoryPackSerializer.Deserialize<MobaEntitasComponentRollbackPayload>(f.Provider.Export(frame));
        var entries = bad.Entries;
        var index = Array.FindIndex(entries, e => e.CastId > 0);
        var entry = entries[index];
        switch (defect)
        {
            case "missing-trailer": entries[index] = new(entry.ActorId, entry.CastId, entry.Data[..^13]); break;
            case "bad-trailer": entry.Data[^13] ^= 1; break;
            case "truncated": entries[index] = new(entry.ActorId, entry.CastId, entry.Data[..3]); break;
            case "duplicate": entries = entries.Append(entry).ToArray(); break;
            case "dual-identity": entries[index] = new(1, entry.CastId, entry.Data); break;
        }
        var payload = MemoryPackSerializer.Serialize(new MobaEntitasComponentRollbackPayload(defect == "v1" ? 1 : 2, entries));
        // Restore valid live state, then perturb a preceding actor. No earlier entry may be applied.
        if (actor.hasSkillCastRetention) actor.RemoveSkillCastRetention();
        cast.ReplaceSkillCastRetention(100);
        cast.isSkillCastRunningTag = false;
        cast.ReplaceSkillCastDestroyRequest(131, 141, SkillDestroyReason.CompletedRetainExpired);
        actor.ReplaceLifetime(99);
        var probe = new Probe();
        var registry = new RollbackRegistry();
        registry.Register(probe);
        registry.Register(f.Provider);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var snapshot = coordinator.Capture(frame);
        for (var i = 0; i < snapshot.Entries.Length; i++)
            if (snapshot.Entries[i].Key == f.Provider.Key) snapshot.Entries[i] = new(f.Provider.Key, payload);
        var live = WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(frame));
        Assert.False(coordinator.TryRestore(snapshot, out var result));
        Assert.Equal(0, result.ProviderCount);
        Assert.Equal(0, probe.Imports);
        Assert.Equal(live, WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(frame)));
        Assert.ThrowsAny<Exception>(() => f.Provider.Import(frame, payload));
        Assert.Equal(live, WorldRollbackSnapshotCodec.Serialize(coordinator.Capture(frame)));
    }

    [Fact]
    public void Already_confirmed_destroyed_cast_still_fails_preflight()
    {
        using var f = new Fixture();
        f.Cast();
        f.Tick(131);
        var frame = new FrameIndex(131);
        var payload = f.Provider.Export(frame);
        f.Frames.Confirmed = 141;
        f.Cleanup.Execute();
        Assert.Throws<InvalidOperationException>(() => f.Provider.ValidateImport(frame, payload));
        Assert.Empty(f.Contexts.actor.GetEntities());
    }

    private static void AssertRequest(ActorEntity cast, int request, int confirmed, SkillDestroyReason reason)
    {
        Assert.True(cast.hasSkillCastDestroyRequest);
        Assert.Equal(request, cast.skillCastDestroyRequest.RequestFrame);
        Assert.Equal(confirmed, cast.skillCastDestroyRequest.MinConfirmedFrame);
        Assert.Equal(reason, cast.skillCastDestroyRequest.Reason);
    }

    private sealed class Probe : IRollbackStateProvider
    {
        public int Key => 1;
        public int Imports;
        public byte[] Export(FrameIndex frame) => new byte[] { 1 };
        public void Import(FrameIndex frame, byte[] payload) => Imports++;
    }

    private sealed class Fixture : IDisposable
    {
        public readonly Contexts Contexts = new();
        public readonly Frames Frames = new();
        private readonly Resolver _services = new();
        private readonly SkillCastCoordinator _skills;
        private readonly EntitasActorIdLookup _lookup;
        public readonly MobaEntitasComponentRollbackProvider Provider;
        public readonly MobaSkillCastDestroyCleanupSystem Cleanup;
        private readonly MobaSkillCastInstanceSyncSystem _sync;

        public Fixture(int retain = 30, int gate = 10)
        {
            var time = new FrameTime();
            _services.Add<IFrameTime>(time);
            _services.Add(new MobaSkillCastInstanceSyncSettings { RetainCompletedFrames = retain, DestroyConfirmGateFrames = gate });
            var authority = new MobaAuthorityFrameService();
            typeof(MobaAuthorityFrameService).GetField("_authorityFrames", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(authority, Frames);
            authority.BindWorld(default);
            _services.Add(authority);
            _lookup = new EntitasActorIdLookup(Contexts.actor);
            var actors = new MobaActorLookupService(new ActorIdIndex(Contexts), new MobaActorRegistry(), new MobaEntityManager(null), Contexts);
            _skills = new SkillCastCoordinator(_services, new Clock(), time, null, new EntitasUnitResolver(_lookup),
                new MobaSkillLoadoutService(null), actors, new TableDrivenMobaSkillPipelineLibrary(null, null));
            _services.Add(_skills);
            Provider = new MobaEntitasComponentRollbackProvider(Contexts.actor);
            _sync = NewSync();
            Cleanup = new MobaSkillCastDestroyCleanupSystem(Contexts, _services);
            Cleanup.Initialize();
        }

        public ActorEntity Cast(SkillCastStage stage = SkillCastStage.Completed, int last = 100, long id = 7)
        {
            var e = Contexts.actor.CreateEntity();
            e.AddSkillCastInstanceId(id);
            e.AddSkillCastStage(stage);
            e.AddSkillCastRetention(last);
            return e;
        }

        public MobaSkillCastInstanceSyncSystem NewSync()
        {
            var system = new MobaSkillCastInstanceSyncSystem(Contexts, _services);
            system.Initialize();
            return system;
        }

        public void Tick(int frame) { Frames.Predicted = frame; _sync.Execute(); Cleanup.Execute(); }

        public SkillPipelineRunner StartRunner()
        {
            var registry = (SkillRunnerRegistry)typeof(SkillCastCoordinator).GetField("_runnerRegistry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_skills)!;
            var runner = registry.GetOrCreate(1);
            var request = new SkillCastRequest(1001, 1, 1, 0, Vec3.Zero, Vec3.Forward, _services, null, null, null);
            var context = new SkillCastContext();
            context.Initialize(in request, 1);
            context.SourceContextId = 7001;
            Assert.True(runner.Start(null, null, new SkillPipelineConfig(1, "retention_test"),
                new IAbilityPipelinePhase<SkillPipelineContext>[] { new AbilityDelayPhase<SkillPipelineContext>(new AbilityPipelinePhaseId("delay"), 1f) },
                new object(), in request, context, out var reason), reason);
            return runner;
        }

        public void Dispose() { _skills.Dispose(); _lookup.Dispose(); Contexts.actor.DestroyAllEntities(); }
    }

    private sealed class Clock : IWorldClock { public float DeltaTime => 0; public float Time => 0; public void Tick(float deltaTime) { } }
    private sealed class Frames : IWorldAuthorityFramesSource
    {
        public int Predicted = 100;
        public int Confirmed;
        public bool TryGetFrames(WorldId world, out FrameIndex confirmed, out FrameIndex predicted)
        { confirmed = new(Confirmed); predicted = new(Predicted); return true; }
    }
    private sealed class Resolver : IWorldResolver
    {
        private readonly Dictionary<Type, object> _values = new();
        public void Add<T>(T value) where T : notnull => _values[typeof(T)] = value;
        public object Resolve(Type type) => _values[type];
        public T Resolve<T>() => (T)Resolve(typeof(T));
        public bool TryResolve(Type type, out object value) => _values.TryGetValue(type, out value!);
        public bool TryResolve<T>(out T value)
        { var found = _values.TryGetValue(typeof(T), out var item); value = found ? (T)item! : default!; return found; }
    }
}
