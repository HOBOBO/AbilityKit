using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaSkillCastRuntimeServiceTests
{
    [Fact]
    public void Skill_runtime_lifecycle_hooks_dispatch_from_snapshot_when_hooks_mutate_registration()
    {
        var hooks = new MobaSkillRuntimeLifecycleHookService();
        var late = new CountingHook();
        var mutating = new MutatingHook(hooks, late);
        var stable = new CountingHook();
        hooks.Register(mutating);
        hooks.Register(stable);

        hooks.Notify(default);

        Assert.Equal(1, mutating.Count);
        Assert.Equal(1, stable.Count);
        Assert.Equal(0, late.Count);

        hooks.Notify(default);

        Assert.Equal(1, mutating.Count);
        Assert.Equal(2, stable.Count);
        Assert.Equal(1, late.Count);
    }

    [Fact]
    public void Skill_runtime_lifecycle_hook_failure_does_not_skip_remaining_snapshot()
    {
        var hooks = new MobaSkillRuntimeLifecycleHookService();
        var failing = new ThrowingHook();
        var stable = new CountingHook();
        hooks.Register(failing);
        hooks.Register(stable);

        var exception = Assert.Throws<InvalidOperationException>(() => hooks.Notify(default));

        Assert.Equal("lifecycle hook failed", exception.Message);
        Assert.Equal(1, failing.Count);
        Assert.Equal(1, stable.Count);
    }

    [Fact]
    public void Skill_runtime_level_parameters_are_frozen_queryable_and_restored_by_both_rollback_paths()
    {
        var service = new MobaSkillCastRuntimeService();
        var snapshot = CreateParameterSnapshot(321f);
        var runtime = service.Create(new MobaSkillCastRuntimeCreateRequest(
            skillId: 2001,
            skillSlot: 1,
            skillLevel: 2,
            sequence: 1,
            casterActorId: 101,
            targetActorId: 202,
            aimPos: Vec3.Zero,
            aimDir: Vec3.Forward,
            rootContextId: 8001,
            levelParameters: snapshot));
        var handle = runtime.Handle;
        var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Projectile, 7001, 8002, 3001);
        Assert.True(service.RetainChild(in handle, in child, out var retain));
        Assert.True(service.TryGetLevelParameter(in handle, "DAMAGE", out var damage));
        Assert.Equal(321f, damage);

        var state = service.CaptureRollbackSnapshot();
        var changed = CreateParameterSnapshot(999f);
        Assert.True(changed.TryGetValue("damage", out var changedDamage));
        Assert.Equal(999f, changedDamage);

        // The retained child keeps the runtime queryable after its cast pipeline has ended.
        Assert.True(service.MarkPipelineEnded(in handle, MobaSkillRuntimeEndReason.PipelineCompleted));
        Assert.True(service.TryGetLevelParameter(in handle, "damage", out damage));
        Assert.Equal(321f, damage);
        Assert.True(service.ReleaseChild(in retain));
        Assert.False(service.TryGetLevelParameter(in handle, "damage", out _));

        service.RestoreRollbackSnapshot(in state); // existing runtime instance path
        Assert.True(service.TryGetLevelParameter(in handle, "damage", out damage));
        Assert.Equal(321f, damage);

        Assert.True(service.ForceTerminate(in handle));
        service.RestoreRollbackSnapshot(in state); // runtime reconstruction path
        Assert.True(service.TryGetLevelParameter(in handle, "damage", out damage));
        Assert.Equal(321f, damage);
        var staleHandle = new MobaSkillCastRuntimeHandle(handle.RuntimeId, handle.Generation + 1, handle.RootContextId);
        Assert.False(service.TryGetLevelParameter(in staleHandle, "damage", out _));
        Assert.False(service.TryGetLevelParameter(in handle, "unknown", out _));
    }

    [Fact]
    public void Skill_runtime_retention_validation_tracks_legacy_runtime_id_api_and_exactly_once_release()
    {
        var service = new MobaSkillCastRuntimeService();
        var runtime = service.Create(new MobaSkillCastRuntimeCreateRequest(
            skillId: 2002,
            skillSlot: 1,
            skillLevel: 1,
            sequence: 1,
            casterActorId: 101,
            targetActorId: 202,
            aimPos: Vec3.Zero,
            aimDir: Vec3.Forward,
            rootContextId: 8002));
        var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Area, 7002, 8003, 3002);

        Assert.True(service.RetainChild(runtime.RuntimeId, in child));
        var retained = service.ValidateRetentionState();
        Assert.True(retained.IsValid);
        Assert.Equal(1, retained.ActiveRuntimes);
        Assert.Equal(1, retained.PendingChildren);
        Assert.Equal(1, retained.RetainHandles);
        Assert.Equal(0, retained.OrphanedRetains);
        Assert.Equal(0, retained.UntrackedChildren);

        Assert.True(service.MarkPipelineEnded(runtime.RuntimeId, MobaSkillRuntimeEndReason.PipelineCompleted));
        Assert.True(service.ReleaseChild(runtime.RuntimeId, in child));
        Assert.False(service.ReleaseChild(runtime.RuntimeId, in child));
        var released = service.ValidateRetentionState();
        Assert.True(released.IsValid);
        Assert.Equal(0, released.ActiveRuntimes);
        Assert.Equal(0, released.PendingChildren);
        Assert.Equal(0, released.RetainHandles);
    }

    [Fact]
    public void Skill_runtime_waits_for_all_retained_children_after_pipeline_end()
    {
        var service = new MobaSkillCastRuntimeService();
        var recorder = new RecordingHook();
        service.LifecycleHooks.Register(recorder);
        var runtime = service.Create(new MobaSkillCastRuntimeCreateRequest(
            skillId: 1001,
            skillSlot: 1,
            skillLevel: 2,
            sequence: 3,
            casterActorId: 101,
            targetActorId: 202,
            aimPos: Vec3.Zero,
            aimDir: Vec3.Zero,
            rootContextId: 9001));
        var handle = runtime.Handle;
        var buff = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Buff, childId: 40001, contextId: 9101, configId: 4001);
        var projectile = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Projectile, childId: 30001, contextId: 9201, configId: 3001);
        var summon = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Summon, childId: 20001, contextId: 9301, configId: 2001);

        Assert.True(service.RetainChild(in handle, in buff, out var buffRetain));
        Assert.True(service.RetainChild(in handle, in projectile, out var projectileRetain));
        Assert.True(service.RetainChild(in handle, in summon, out var summonRetain));
        Assert.True(service.MarkPipelineEnded(in handle, MobaSkillRuntimeEndReason.PipelineCompleted));

        Assert.True(service.TryGet(in handle, out _));
        Assert.True(service.TryGetDiagnostics(in handle, out var pendingDiagnostics));
        Assert.True(pendingDiagnostics.IsWaitingChildren);
        Assert.Equal(3, pendingDiagnostics.PendingChildren);
        Assert.Equal(1, service.CountPendingChildren(in handle, MobaSkillRuntimeChildKind.Summon));

        Assert.True(service.ReleaseChild(in buffRetain));
        Assert.True(service.ReleaseChild(in projectileRetain));
        Assert.True(service.TryGet(in handle, out _));
        Assert.True(service.TryGetDiagnostics(in handle, out var partialDiagnostics));
        Assert.Equal(1, partialDiagnostics.PendingChildren);
        Assert.Equal(MobaSkillRuntimeChildKind.Summon, partialDiagnostics.Children[0].Kind);

        Assert.True(service.ReleaseChild(in summonRetain));

        Assert.False(service.TryGet(in handle, out _));
        Assert.Contains(MobaSkillRuntimeLifecycleEventKind.Created, recorder.Events);
        Assert.Equal(3, recorder.Count(MobaSkillRuntimeLifecycleEventKind.ChildRetained));
        Assert.Equal(3, recorder.Count(MobaSkillRuntimeLifecycleEventKind.ChildReleased));
        Assert.Contains(MobaSkillRuntimeLifecycleEventKind.PipelineEnded, recorder.Events);
        Assert.Contains(MobaSkillRuntimeLifecycleEventKind.WaitingChildren, recorder.Events);
        Assert.Contains(MobaSkillRuntimeLifecycleEventKind.Finalizing, recorder.Events);
        Assert.Contains(MobaSkillRuntimeLifecycleEventKind.Finalized, recorder.Events);
        Assert.Equal(MobaSkillRuntimeEndReason.PipelineCompleted, recorder.LastReason(MobaSkillRuntimeLifecycleEventKind.WaitingChildren));
        Assert.False(recorder.LastForced(MobaSkillRuntimeLifecycleEventKind.WaitingChildren));
    }

    [Fact]
    public void Skill_runtime_force_terminate_ignores_pending_children_and_clears_retains()
    {
        var service = new MobaSkillCastRuntimeService();
        var runtime = service.Create(new MobaSkillCastRuntimeCreateRequest(
            skillId: 1002,
            skillSlot: 1,
            skillLevel: 1,
            sequence: 1,
            casterActorId: 101,
            targetActorId: 202,
            aimPos: Vec3.Zero,
            aimDir: Vec3.Zero,
            rootContextId: 0));
        var handle = runtime.Handle;
        var summon = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Summon, childId: 20002, contextId: 9302, configId: 2002);

        Assert.True(service.RetainChild(in handle, in summon, out var retain));
        var recorder = new RecordingHook();
        service.LifecycleHooks.Register(recorder);

        Assert.True(service.ForceTerminate(in handle));

        Assert.False(service.TryGet(in handle, out _));
        Assert.False(service.ReleaseChild(in retain));
        Assert.Contains(MobaSkillRuntimeLifecycleEventKind.ForceTerminated, recorder.Events);
        Assert.Contains(MobaSkillRuntimeLifecycleEventKind.Finalized, recorder.Events);
        Assert.Equal(MobaSkillRuntimeEndReason.RollbackCleanup, recorder.LastReason(MobaSkillRuntimeLifecycleEventKind.ForceTerminated));
        Assert.True(recorder.LastForced(MobaSkillRuntimeLifecycleEventKind.Finalized));
    }

    [Fact]
    public void Skill_runtime_scan_reports_waiting_children_metrics_and_warning()
    {
        var service = new MobaSkillCastRuntimeService();
        var diagnostics = new TestBattleDiagnosticsService();
        var runtime = service.Create(new MobaSkillCastRuntimeCreateRequest(
            skillId: 1003,
            skillSlot: 2,
            skillLevel: 1,
            sequence: 4,
            casterActorId: 101,
            targetActorId: 202,
            aimPos: Vec3.Zero,
            aimDir: Vec3.Zero,
            rootContextId: 9401));
        var handle = runtime.Handle;
        var buff = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Buff, childId: 40003, contextId: 9501, configId: 4003);

        Assert.True(service.RetainChild(in handle, in buff, out _));
        Assert.True(service.MarkPipelineEnded(in handle, MobaSkillRuntimeEndReason.PipelineCompleted));

        var result = service.ScanDiagnostics(diagnostics);

        Assert.Equal(1, result.ActiveRuntimes);
        Assert.Equal(1, result.WaitingChildrenRuntimes);
        Assert.Equal(1, result.PendingChildren);
        Assert.True(result.HasWaitingChildren);
        Assert.Equal(1, diagnostics.Gauges[MobaBattleDiagnosticMetric.SkillRuntimeActive]);
        Assert.Equal(1, diagnostics.Gauges[MobaBattleDiagnosticMetric.SkillRuntimeWaitingChildren]);
        Assert.Equal(1, diagnostics.Gauges[MobaBattleDiagnosticMetric.SkillRuntimePendingChildren]);
        Assert.Contains(diagnostics.Warnings, item => item.Key.StartsWith("moba.skill.runtime.waitingChildren."));
    }

    [Fact]
    public void Skill_runtime_bridge_forwards_to_generic_runtime_lifecycle_hooks()
    {
        var service = new MobaSkillCastRuntimeService();
        var runtimeHooks = new MobaRuntimeLifecycleHookService();
        var recorder = new RecordingRuntimeHook();
        runtimeHooks.Register(recorder);
        service.LifecycleHooks.Register(MobaRuntimeLifecycleHookFactory.CreateSkillRuntimeBridge(runtimeHooks));

        var runtime = service.Create(new MobaSkillCastRuntimeCreateRequest(
            skillId: 1004,
            skillSlot: 2,
            skillLevel: 1,
            sequence: 5,
            casterActorId: 101,
            targetActorId: 202,
            aimPos: Vec3.Zero,
            aimDir: Vec3.Zero,
            rootContextId: 9601));
        var handle = runtime.Handle;

        Assert.True(service.MarkPipelineEnded(in handle, MobaSkillRuntimeEndReason.PipelineCompleted));

        Assert.Equal(MobaRuntimeLifecycleEventKind.Activated, recorder.Events[0]);
        Assert.Contains(MobaRuntimeLifecycleEventKind.Ended, recorder.Events);
        Assert.Equal(9601, recorder.Sources[0].SourceContextId);
        Assert.Equal(MobaContextSourceBoundary.LiveRuntime, recorder.Sources[0].Boundary);
    }

    private sealed class RecordingHook : IMobaSkillRuntimeLifecycleHook
    {
        public readonly List<MobaSkillRuntimeLifecycleEventKind> Events = new();
        private readonly List<MobaSkillRuntimeLifecycleEvent> _events = new();

        public void OnSkillRuntimeLifecycle(in MobaSkillRuntimeLifecycleEvent lifecycleEvent)
        {
            Events.Add(lifecycleEvent.Kind);
            _events.Add(lifecycleEvent);
        }

        public int Count(MobaSkillRuntimeLifecycleEventKind kind)
        {
            var count = 0;
            for (var i = 0; i < Events.Count; i++)
            {
                if (Events[i] == kind) count++;
            }

            return count;
        }

        public MobaSkillRuntimeEndReason LastReason(MobaSkillRuntimeLifecycleEventKind kind)
        {
            for (var i = _events.Count - 1; i >= 0; i--)
            {
                if (_events[i].Kind == kind) return _events[i].Reason;
            }

            return MobaSkillRuntimeEndReason.None;
        }

        public bool LastForced(MobaSkillRuntimeLifecycleEventKind kind)
        {
            for (var i = _events.Count - 1; i >= 0; i--)
            {
                if (_events[i].Kind == kind) return _events[i].Forced;
            }

            return false;
        }
    }

    private sealed class MutatingHook : IMobaSkillRuntimeLifecycleHook
    {
        private readonly MobaSkillRuntimeLifecycleHookService _hooks;
        private readonly IMobaSkillRuntimeLifecycleHook _late;

        public MutatingHook(MobaSkillRuntimeLifecycleHookService hooks, IMobaSkillRuntimeLifecycleHook late)
        {
            _hooks = hooks;
            _late = late;
        }

        public int Count { get; private set; }

        public void OnSkillRuntimeLifecycle(in MobaSkillRuntimeLifecycleEvent lifecycleEvent)
        {
            Count++;
            _hooks.Unregister(this);
            _hooks.Register(_late);
        }
    }

    private sealed class CountingHook : IMobaSkillRuntimeLifecycleHook
    {
        public int Count { get; private set; }

        public void OnSkillRuntimeLifecycle(in MobaSkillRuntimeLifecycleEvent lifecycleEvent)
        {
            Count++;
        }
    }

    private sealed class ThrowingHook : IMobaSkillRuntimeLifecycleHook
    {
        public int Count { get; private set; }

        public void OnSkillRuntimeLifecycle(in MobaSkillRuntimeLifecycleEvent lifecycleEvent)
        {
            Count++;
            throw new InvalidOperationException("lifecycle hook failed");
        }
    }

    private static MobaSkillLevelParameterSnapshot CreateParameterSnapshot(float damage)
    {
        var table = new SkillLevelTableMO(new SkillLevelTableDTO
        {
            Id = 9001,
            ParamNames = new[] { "damage" },
            Levels = new[] { new SkillLevelDTO { Params = new[] { damage } } }
        });
        Assert.True(table.TryCreateParameterSnapshot(1, 2001, out var snapshot, out var error), error);
        return snapshot;
    }

    private sealed class RecordingRuntimeHook : IMobaRuntimeLifecycleHook
    {
        public readonly List<MobaRuntimeLifecycleEventKind> Events = new();
        public readonly List<MobaContextSourceView> Sources = new();

        public void OnRuntimeLifecycle(in MobaRuntimeLifecycleEvent lifecycleEvent)
        {
            Events.Add(lifecycleEvent.Kind);
            Sources.Add(lifecycleEvent.Source);
        }
    }

    private sealed class TestBattleDiagnosticsService : IMobaBattleDiagnosticsService
    {
        public readonly Dictionary<string, long> Gauges = new();
        public readonly List<KeyValuePair<string, string>> Warnings = new();

        public long GetTimestamp() => 0L;
        public bool IsEnabled(string channel) => true;
        public bool ShouldSample(string channel) => true;
        public MobaBattleDiagnosticScope Measure(string metricName, double warnThresholdMs = 0d, string context = null) => default;
        public void RecordDuration(string metricName, long startTimestamp, double warnThresholdMs = 0d, string context = null) { }
        public void Counter(string counterName, long value = 1L) { }
        public void Gauge(string gaugeName, long value) => Gauges[gaugeName] = value;
        public void Sample(string sampleName, double value) { }
        public void Warning(string key, string message, int maxCount = MobaBattleDiagnosticsDefaults.DefaultWarningLimit) => Warnings.Add(new KeyValuePair<string, string>(key, message));
        public void Warning(string key, string message, in MobaBattleDiagnosticContext context, int maxCount = MobaBattleDiagnosticsDefaults.DefaultWarningLimit) => Warning(key, message, maxCount);
        public void Warning(string key, Func<string> messageFactory, int maxCount = MobaBattleDiagnosticsDefaults.DefaultWarningLimit) => Warnings.Add(new KeyValuePair<string, string>(key, messageFactory != null ? messageFactory() : null));
        public void Warning(string key, Func<string> messageFactory, in MobaBattleDiagnosticContext context, int maxCount = MobaBattleDiagnosticsDefaults.DefaultWarningLimit) => Warning(key, messageFactory, maxCount);
        public void Exception(string key, Exception exception, string context, int maxCount = MobaBattleDiagnosticsDefaults.DefaultExceptionLimit) { }
        public void Exception(string key, Exception exception, string context, in MobaBattleDiagnosticContext diagnosticContext, int maxCount = MobaBattleDiagnosticsDefaults.DefaultExceptionLimit) { }
        public void RecordInputBatchAccepted(int acceptedCount, int handledCount) { }
        public void RecordInputCommandRejected(MobaInputCommandFailureCode failureCode) { }
        public void RecordInputCommandException() { }
        public void RecordSnapshotRouterHealth(in MobaSnapshotRouterHealth health) { }
        public void RecordLifecycleHealth(in MobaTemporaryEntityLifecycleHealth health) { }
        public IReadOnlyList<MobaBattleDiagnosticWarningRecord> GetWarningsSnapshot() => Array.Empty<MobaBattleDiagnosticWarningRecord>();
        public IReadOnlyList<MobaBattleDiagnosticExceptionRecord> GetExceptionsSnapshot() => Array.Empty<MobaBattleDiagnosticExceptionRecord>();
        public MobaBattleDiagnosticsSnapshot GetSnapshot() => default;
    }
}
