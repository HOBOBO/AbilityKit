using System.Linq;
using AbilityKit.Combat.Projectile;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Trace;
using AbilityKit.Pipeline;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Trace;

public sealed class MobaTraceRegistrySmokeTests
{
    [Fact]
    public void Skill_phase_location_is_preserved_on_trace_metadata()
    {
        using var registry = new MobaTraceRegistry();
        var rootId = registry.CreateRootContext(MobaTraceKind.SkillCast, 1001, 1, 2);
        var phaseId = registry.CreateChildContext(rootId, MobaTraceKind.SkillPhase, 1001, 1, 2);

        Assert.True(registry.TrySetSkillPhaseLocation(phaseId, 1001, 3001, "cast.release"));
        Assert.True(registry.TryGetNodeSnapshot(phaseId, out var snapshot));
        var metadata = Assert.IsType<MobaTraceMetadata>(snapshot.Metadata);
        Assert.Equal(1001, metadata.SkillId);
        Assert.Equal(3001, metadata.CastFlowId);
        Assert.Equal("cast.release", metadata.PhaseId);
    }

    [Fact]
    public void Pipeline_recorder_creates_and_completes_real_skill_phase_trace()
    {
        using var registry = new MobaTraceRegistry();
        var rootId = registry.CreateRootContext(MobaTraceKind.SkillCast, 1001, 1, 2);
        var parentId = registry.CreateChildContext(rootId, MobaTraceKind.SkillPhase, 1001, 1, 2);
        var resolver = new TestWorldResolver(registry);
        var aimPos = Vec3.Zero;
        var aimDir = Vec3.Forward;
        var request = new SkillCastRequest(1001, 1, 1, 2, in aimPos, in aimDir, resolver, null, null, null);
        var castContext = new SkillCastContext { CastFlowId = 3001, SourceContextId = rootId };
        var context = new SkillPipelineContext();
        context.Initialize(null, in request, castContext);
        context.SetPipelineTraceLocation(3001, parentId);
        var owner = new TestPipelineRun(42, context);
        var recorder = new MobaPipelineDiagnosticsRecorder(new TestBattleDiagnosticsService());
        var pipelinePhaseId = new AbilityPipelinePhaseId("cast.release");

        recorder.Record(owner, new PipelineTraceData(1, EPipelineTraceEventType.PhaseStart, pipelinePhaseId, EAbilityPipelineState.Executing, "phase"));

        var active = registry.GetNodeSnapshotsByRoot(rootId)
            .Single(node => node.ParentId == parentId &&
                            node.Metadata is MobaTraceMetadata metadata &&
                            metadata.PhaseId == "cast.release");
        var activeMetadata = Assert.IsType<MobaTraceMetadata>(active.Metadata);
        Assert.False(active.IsEnded);
        Assert.Equal(1001, activeMetadata.SkillId);
        Assert.Equal(3001, activeMetadata.CastFlowId);

        recorder.Record(owner, new PipelineTraceData(2, EPipelineTraceEventType.PhaseComplete, pipelinePhaseId, EAbilityPipelineState.Executing, "phase"));

        Assert.True(registry.TryGetNodeSnapshot(active.ContextId, out var ended));
        Assert.True(ended.IsEnded);
        Assert.Equal((int)TraceLifecycleReason.Completed, ended.EndReason);
    }

    [Fact]
    public void Registry_can_create_query_snapshot_and_end_moba_trace_chain()
    {
        using var registry = new MobaTraceRegistry();
        var events = new List<TraceRegistryEvent>();
        registry.RegistryEvent += events.Add;

        var rootId = registry.CreateRootContext(
            MobaTraceKind.SkillEffect,
            configId: 1001,
            sourceActorId: 1,
            targetActorId: 2);
        var childId = registry.CreateChildContext(
            rootId,
            MobaTraceKind.EffectAction,
            configId: 2001,
            sourceActorId: 1,
            targetActorId: 2);

        Assert.True(rootId > 0);
        Assert.True(childId > 0);
        Assert.True(registry.TryGetNodeSnapshot(rootId, out var rootSnapshot));
        Assert.True(registry.TryGetNodeSnapshot(childId, out var childSnapshot));
        Assert.True(rootSnapshot.IsRoot);
        Assert.Equal(rootId, childSnapshot.RootId);
        Assert.Equal(rootId, childSnapshot.ParentId);
        Assert.Equal((int)MobaTraceKind.SkillEffect, rootSnapshot.Kind);
        Assert.Equal((int)MobaTraceKind.EffectAction, childSnapshot.Kind);

        var chain = registry.GetChain(rootId);
        Assert.Equal(2, chain.Count);
        Assert.Contains(chain, item => item.ContextId == rootId && item.Metadata.ConfigId == 1001);
        Assert.Contains(chain, item => item.ContextId == childId && item.Kind == (int)MobaTraceKind.EffectAction);

        var snapshots = registry.GetNodeSnapshotsByRoot(rootId).ToArray();
        Assert.Equal(2, snapshots.Length);
        Assert.Contains(snapshots, item => item.ContextId == childId);

        Assert.True(registry.EndRoot(rootId, (int)TraceLifecycleReason.Completed) > 0);
        Assert.True(registry.TryGetNodeSnapshot(rootId, out var endedRoot));
        Assert.Equal(rootId, endedRoot.ContextId);
        Assert.Contains(events, item => item.Kind == TraceRegistryEventKind.RootCreated && item.ContextId == rootId);
        Assert.Contains(events, item => item.Kind == TraceRegistryEventKind.ChildCreated && item.ContextId == childId);
        Assert.Contains(events, item => item.Kind == TraceRegistryEventKind.RootEnded && item.ContextId == rootId);
    }

    [Fact]
    public void Export_root_can_prune_nodes_and_strip_metadata()
    {
        using var registry = new MobaTraceRegistry();
        var rootId = registry.CreateRootContext(
            MobaTraceKind.SkillEffect,
            configId: 1001,
            sourceActorId: 1,
            targetActorId: 2);
        var childId = registry.CreateChildContext(
            rootId,
            MobaTraceKind.BuffApply,
            configId: 2001,
            sourceActorId: 1,
            targetActorId: 2);

        var full = registry.ExportRoot(rootId, TraceExportOptions.Full);
        var pruned = registry.ExportRoot(rootId, new TraceExportOptions(maxNodes: 1, activeOnly: false, includeMetadata: false));

        Assert.Equal(rootId, full.RootId);
        Assert.Equal(2, full.Nodes.Count);
        Assert.False(full.Truncated);
        Assert.Contains(full.Nodes, item => item.ContextId == rootId && item.KindName == nameof(MobaTraceKind.SkillEffect) && item.Metadata != null);
        Assert.Contains(full.Nodes, item => item.ContextId == childId && item.ParentId == rootId && item.Metadata != null);

        Assert.Equal(rootId, pruned.RootId);
        Assert.Single(pruned.Nodes);
        Assert.True(pruned.Truncated);
        Assert.Null(pruned.Nodes[0].Metadata);
    }

    [Fact]
    public void Export_root_can_apply_depth_and_tree_order_options()
    {
        using var registry = new MobaTraceRegistry();
        var rootId = registry.CreateRootContext(
            MobaTraceKind.SkillEffect,
            configId: 1001,
            sourceActorId: 1,
            targetActorId: 2);
        var childId = registry.CreateChildContext(
            rootId,
            MobaTraceKind.EffectAction,
            configId: 2001,
            sourceActorId: 1,
            targetActorId: 2);
        var grandChildId = registry.CreateChildContext(
            childId,
            MobaTraceKind.BuffTick,
            configId: 3001,
            sourceActorId: 1,
            targetActorId: 2);

        var export = registry.ExportRoot(
            rootId,
            new TraceExportOptions(
                maxNodes: 0,
                activeOnly: false,
                includeMetadata: true,
                maxDepth: 1,
                order: TraceExportOrder.TreePreOrder));

        Assert.True(export.Truncated);
        Assert.Equal(new[] { rootId, childId }, export.Nodes.Select(item => item.ContextId).ToArray());
        Assert.DoesNotContain(export.Nodes, item => item.ContextId == grandChildId);
    }

    [Fact]
    public void Projectile_source_snapshot_survives_link_cleanup_while_trace_remains_until_purge()
    {
        using var registry = new MobaTraceRegistry();
        var rootId = registry.CreateRootContext(
            MobaTraceKind.SkillCast,
            configId: 1001,
            sourceActorId: 101,
            targetActorId: 202);
        var launchContextId = registry.CreateChildContext(
            rootId,
            MobaTraceKind.ProjectileLaunch,
            configId: 3001,
            sourceActorId: 101,
            targetActorId: 202);
        var projectileId = new ProjectileId(7001);
        var links = new MobaProjectileLinkService();
        var source = ProjectileSourceContextBuilder.Create()
            .WithActors(101, 202)
            .WithProjectileConfig(3001)
            .WithSourceContext(launchContextId)
            .WithRootContext(rootId)
            .WithOwnerContext(rootId)
            .Build();

        links.Link(projectileId, actorId: 9001);
        links.BindSource(projectileId, in source);
        Assert.True(links.TryGetSource(projectileId, out var linkedSource));
        Assert.Equal(launchContextId, linkedSource.SourceContextId);
        Assert.True(linkedSource.TryGetLineageContext(out var lineage));
        Assert.Equal(rootId, lineage.RootContextId);

        Assert.True(registry.EndContext(launchContextId, TraceLifecycleReason.Completed));
        links.UnlinkByActorId(actorId: 9001);

        Assert.False(links.TryGetSource(projectileId, out _));
        Assert.True(source.TryGetPersistentContextSource(out var persistentSource));
        Assert.True(persistentSource.HasExecutionSource);
        Assert.True(persistentSource.TryGetContextSource(out var sourceView));
        Assert.Equal(MobaContextSourceBoundary.Snapshot, sourceView.Boundary);
        Assert.False(sourceView.HasLiveRuntime);
        Assert.Equal(launchContextId, sourceView.SourceContextId);
        Assert.True(persistentSource.TryGetLineageContext(out var persistentLineage));
        Assert.Equal(rootId, persistentLineage.RootContextId);
        Assert.True(persistentSource.TryGetContextSource(out var endedSourceView));
        Assert.Equal(launchContextId, endedSourceView.SourceContextId);
        Assert.Equal(rootId, endedSourceView.RootContextId);

        registry.PurgeRoot(rootId);
        Assert.False(registry.TryGetNodeSnapshot(launchContextId, out _));
    }

    [Fact]
    public void Retained_persistent_source_blocks_purge_until_handle_is_released()
    {
        using var registry = new MobaTraceRegistry();
        var rootId = registry.CreateRootContext(
            MobaTraceKind.BuffApply,
            configId: 4001,
            sourceActorId: 101,
            targetActorId: 202);
        var childId = registry.CreateChildContext(
            rootId,
            MobaTraceKind.BuffTick,
            configId: 4001,
            sourceActorId: 101,
            targetActorId: 202);
        var source = new MobaContextSourceView(
            MobaContextSourceResolveKind.DirectProvider,
            MobaContextSourceBoundary.Snapshot,
            EffectContextKind.Buff,
            MobaTraceKind.BuffTick,
            sourceActorId: 101,
            targetActorId: 202,
            sourceContextId: childId,
            parentContextId: rootId,
            rootContextId: rootId,
            ownerContextId: rootId,
            configId: 4001,
            triggerId: 0,
            frame: 0,
            runtimeKind: MobaRuntimeKindNames.Buff,
            runtimeConfigId: 4001,
            hasLiveRuntime: false,
            skillRuntimeHandle: default);
        var snapshot = MobaPersistentContextSourceSnapshotFactory.FromContextSource(in source);

        Assert.True(registry.TryRetainPersistentSource(in snapshot, "buff.tick.delayed", out var handle));
        Assert.True(handle.IsValid);
        Assert.Equal(rootId, handle.RootId);
        Assert.True(registry.EndContext(childId, TraceLifecycleReason.Completed));
        Assert.True(registry.EndContext(rootId, TraceLifecycleReason.Completed));

        var purgedWhileRetained = registry.Purge(currentFrame: 100, keepEndedFrames: 0);

        Assert.Equal(0, purgedWhileRetained);
        Assert.True(registry.TryGetNodeSnapshot(childId, out _));
        Assert.True(registry.TryGetRootState(rootId, out var retainedState));
        Assert.Equal(1, retainedState.ExternalRefCount);

        handle.Dispose();
        handle.Dispose();
        Assert.True(registry.TryGetRootState(rootId, out var releasedState));
        Assert.Equal(0, releasedState.ExternalRefCount);

        var purgedAfterRelease = registry.Purge(currentFrame: 100, keepEndedFrames: 0);

        Assert.Equal(1, purgedAfterRelease);
        Assert.False(registry.TryGetNodeSnapshot(childId, out _));
    }

    [Fact]
    public void Retention_scan_reports_retained_ended_and_stale_roots()
    {
        using var registry = new MobaTraceRegistry();
        var diagnostics = new TestBattleDiagnosticsService();
        var rootId = registry.CreateRootContext(
            MobaTraceKind.ProjectileLaunch,
            configId: 3001,
            sourceActorId: 101,
            targetActorId: 202);
        var source = new MobaContextSourceView(
            MobaContextSourceResolveKind.DirectProvider,
            MobaContextSourceBoundary.Snapshot,
            EffectContextKind.Projectile,
            MobaTraceKind.ProjectileLaunch,
            sourceActorId: 101,
            targetActorId: 202,
            sourceContextId: rootId,
            parentContextId: rootId,
            rootContextId: rootId,
            ownerContextId: rootId,
            configId: 3001,
            triggerId: 0,
            frame: 0,
            runtimeKind: MobaRuntimeKindNames.Projectile,
            runtimeConfigId: 3001,
            hasLiveRuntime: false,
            skillRuntimeHandle: default);
        var snapshot = MobaPersistentContextSourceSnapshotFactory.FromContextSource(in source);

        Assert.True(registry.TryRetainPersistentSource(in snapshot, "projectile.hit.delay", out var handle));
        Assert.True(registry.EndRoot(rootId, (int)TraceLifecycleReason.Completed) > 0);

        var result = registry.ScanRetention(diagnostics, staleFrameThreshold: 0, currentFrame: 20);

        Assert.Equal(1, result.TotalRoots);
        Assert.Equal(1, result.RetainedRoots);
        Assert.Equal(0, result.RetainedEndedRoots);
        Assert.Equal(0, result.StaleRetainedRoots);
        Assert.True(result.TotalNodes >= 0);
        Assert.True(result.EndedNodes >= 0);
        Assert.Equal(1, diagnostics.Gauges[MobaBattleDiagnosticMetric.TraceRetainedRoots]);
        Assert.Equal(0, diagnostics.Gauges[MobaBattleDiagnosticMetric.TraceRetainedEndedRoots]);
        Assert.DoesNotContain(diagnostics.Warnings, item => item.Key.StartsWith("moba.trace.retention.stale."));

        handle.Dispose();
    }

    [Fact]
    public void Lifecycle_hook_trace_retention_survives_runtime_context_cleanup_until_release()
    {
        using var registry = new MobaTraceRegistry();
        var hook = new MobaTraceRetentionLifecycleHook(registry);
        var rootId = registry.CreateRootContext(
            MobaTraceKind.SkillEffect,
            configId: 1001,
            sourceActorId: 101,
            targetActorId: 202);
        var buffContextId = registry.CreateChildContext(
            rootId,
            MobaTraceKind.BuffApply,
            configId: 4001,
            sourceActorId: 101,
            targetActorId: 202);
        var source = new MobaContextSourceView(
            MobaContextSourceResolveKind.DirectProvider,
            MobaContextSourceBoundary.Snapshot,
            EffectContextKind.Buff,
            MobaTraceKind.BuffApply,
            sourceActorId: 101,
            targetActorId: 202,
            sourceContextId: buffContextId,
            parentContextId: rootId,
            rootContextId: rootId,
            ownerContextId: buffContextId,
            configId: 4001,
            triggerId: 0,
            frame: 0,
            runtimeKind: MobaRuntimeKindNames.Buff,
            runtimeConfigId: 4001,
            hasLiveRuntime: true,
            skillRuntimeHandle: default);
        var runtime = new BuffRuntime
        {
            BuffId = 4001,
            SourceId = 101,
            SourceContextId = buffContextId,
            ContextSource = source,
        };
        var activated = new MobaRuntimeLifecycleEvent(MobaRuntimeLifecycleEventKind.Activated, runtime, in source, "buff.lifecycle.active");

        hook.OnRuntimeLifecycle(in activated);
        Assert.True(hook.IsRetained(runtime));
        Assert.True(registry.EndContext(buffContextId, TraceLifecycleReason.Completed));
        Assert.True(registry.EndContext(rootId, TraceLifecycleReason.Completed));

        runtime.SourceContextId = 0;
        runtime.ContextSource = default;
        runtime.Origin = default;
        var purgedWhileRuntimeCleaned = registry.PurgeReleased(currentFrame: 100, keepEndedFrames: 0);

        Assert.Equal(0, purgedWhileRuntimeCleaned);
        Assert.True(registry.TryGetNodeSnapshot(buffContextId, out _));
        Assert.True(registry.TryGetRootState(rootId, out var retainedState));
        Assert.Equal(1, retainedState.ExternalRefCount);

        var ended = new MobaRuntimeLifecycleEvent(MobaRuntimeLifecycleEventKind.Ended, runtime, in source, "buff.lifecycle.ended");
        hook.OnRuntimeLifecycle(in ended);
        Assert.False(hook.IsRetained(runtime));
        var purgedAfterRelease = registry.PurgeReleased(currentFrame: 100, keepEndedFrames: 0);

        Assert.Equal(1, purgedAfterRelease);
        Assert.False(registry.TryGetNodeSnapshot(buffContextId, out _));
    }

    [Fact]
    public void Safe_purge_root_refuses_retained_roots_until_release()
    {
        using var registry = new MobaTraceRegistry();
        var rootId = registry.CreateRootContext(
            MobaTraceKind.ProjectileLaunch,
            configId: 3001,
            sourceActorId: 101,
            targetActorId: 202);
        var source = new MobaContextSourceView(
            MobaContextSourceResolveKind.DirectProvider,
            MobaContextSourceBoundary.Snapshot,
            EffectContextKind.Projectile,
            MobaTraceKind.ProjectileLaunch,
            sourceActorId: 101,
            targetActorId: 202,
            sourceContextId: rootId,
            parentContextId: rootId,
            rootContextId: rootId,
            ownerContextId: rootId,
            configId: 3001,
            triggerId: 0,
            frame: 0,
            runtimeKind: MobaRuntimeKindNames.Projectile,
            runtimeConfigId: 3001,
            hasLiveRuntime: false,
            skillRuntimeHandle: default);
        var snapshot = MobaPersistentContextSourceSnapshotFactory.FromContextSource(in source);

        Assert.True(registry.TryRetainPersistentSource(in snapshot, "projectile.hit.delay", out var handle));
        Assert.True(registry.EndContext(rootId, TraceLifecycleReason.Completed));

        Assert.False(registry.TryPurgeReleasedRoot(rootId));
        Assert.True(registry.TryGetNodeSnapshot(rootId, out _));

        handle.Dispose();
        Assert.True(registry.TryPurgeReleasedRoot(rootId));
        Assert.False(registry.TryGetNodeSnapshot(rootId, out _));
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

    private sealed class TestWorldResolver : IWorldResolver
    {
        private readonly MobaTraceRegistry _trace;

        public TestWorldResolver(MobaTraceRegistry trace) => _trace = trace;

        public object Resolve(Type serviceType) =>
            serviceType == typeof(MobaTraceRegistry) ? _trace : null;

        public T Resolve<T>() => TryResolve<T>(out var instance) ? instance : default;

        public bool TryResolve(Type serviceType, out object instance)
        {
            instance = Resolve(serviceType);
            return instance != null;
        }

        public bool TryResolve<T>(out T instance)
        {
            if (_trace is T resolved)
            {
                instance = resolved;
                return true;
            }

            instance = default;
            return false;
        }
    }

    private sealed class TestPipelineRun : IAbilityPipelineRun<SkillPipelineContext>, IPipelineLifeOwner
    {
        public TestPipelineRun(int ownerId, SkillPipelineContext context)
        {
            OwnerId = ownerId;
            Context = context;
        }

        public int OwnerId { get; }
        public string OwnerName => "test-skill-pipeline";
        public EAbilityPipelineState State { get; private set; } = EAbilityPipelineState.Executing;
        public SkillPipelineContext Context { get; }
        public AbilityPipelinePhaseId CurrentPhaseId => Context.CurrentPhaseId;
        public bool IsPaused => false;
        public IReadOnlyList<AbilityPipelinePhaseId> ActivePhases => Array.Empty<AbilityPipelinePhaseId>();
        public void Tick(float deltaTime) { }
        public void Pause() { }
        public void Resume() { }
        public void Interrupt() => State = EAbilityPipelineState.Failed;
        public void Cancel() { }
    }
}
