using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Trace;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaBattleDiagnosticTraceReadStoreTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 1);
        }

        [Test]
        public void QueryTrace_UsesRegistryFramesParentChainAndTreePreOrder()
        {
            var collector = MakeCollector();
            var registry = new MobaTraceRegistry();
            var frameTime = new FrameTime();
            frameTime.Reset(new FrameIndex(10), 0f, 0.02f);
            registry.AttachFrameTime(frameTime);
            var store = new MobaBattleDiagnosticTraceReadStore(registry, collector.Store);

            var rootId = registry.CreateRootContext(MobaTraceKind.SkillCast, 501, 7, 21);
            frameTime.StepTo(new FrameIndex(11), 0.02f);
            var firstChildId = registry.CreateChildContext(rootId, MobaTraceKind.SkillPhase, 502, 7, 21);
            var secondChildId = registry.CreateChildContext(rootId, MobaTraceKind.EffectExecution, 503, 8, 22);
            frameTime.StepTo(new FrameIndex(12), 0.02f);
            var grandChildId = registry.CreateChildContext(firstChildId, MobaTraceKind.EffectAction, 504, 9, 23);
            frameTime.StepTo(new FrameIndex(15), 0.02f);
            registry.EndContext(grandChildId, TraceLifecycleReason.Completed);

            var result = store.QueryTrace(1, rootId);

            Assert.That(result.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
            Assert.That(result.Status.StoreRevision, Is.EqualTo(registry.Revision));
            Assert.That(result.Items.Count, Is.EqualTo(4));
            Assert.That(result.Items[0].ContextId, Is.EqualTo(rootId));
            Assert.That(result.Items[1].ContextId, Is.EqualTo(firstChildId));
            Assert.That(result.Items[2].ContextId, Is.EqualTo(grandChildId));
            Assert.That(result.Items[3].ContextId, Is.EqualTo(secondChildId));
            Assert.That(result.Items[0].StartFrame, Is.EqualTo(10));
            Assert.That(result.Items[1].StartFrame, Is.EqualTo(11));
            Assert.That(result.Items[2].StartFrame, Is.EqualTo(12));
            Assert.That(result.Items[2].EndFrame, Is.EqualTo(15));
            Assert.That(result.Items[2].ParentContextId, Is.EqualTo(firstChildId));
            Assert.That(result.Items[2].State, Is.EqualTo(BattleDiagnosticTraceNodeState.Ended));
            Assert.That(result.Items[2].EndReason, Is.EqualTo(nameof(TraceLifecycleReason.Completed)));
            Assert.That(result.Items[2].ActorId, Is.EqualTo(9));
            Assert.That(result.Items[2].ConfigId, Is.EqualTo(504));
            Assert.That(result.Items[3].State, Is.EqualTo(BattleDiagnosticTraceNodeState.Active));
            Assert.That(result.Items[3].EndFrame, Is.EqualTo(BattleDiagnosticFrames.Invalid));
        }

        [Test]
        public void CaptureTraceSnapshot_ExportsAllRootsInTreePreOrderWithStableRevision()
        {
            var collector = MakeCollector();
            var registry = new MobaTraceRegistry();
            var firstRootId = registry.CreateRootContext(MobaTraceKind.SkillCast, 501);
            var firstChildId = registry.CreateChildContext(
                firstRootId,
                MobaTraceKind.SkillPhase,
                502);
            var secondRootId = registry.CreateRootContext(MobaTraceKind.EffectExecution, 503);
            var store = new MobaBattleDiagnosticTraceReadStore(registry, collector.Store);

            var snapshot = store.CaptureTraceSnapshot();
            registry.CreateChildContext(secondRootId, MobaTraceKind.EffectAction, 504);

            Assert.That(snapshot.Revision, Is.EqualTo(3));
            Assert.That(snapshot.IsStable, Is.True);
            Assert.That(snapshot.Truncated, Is.False);
            Assert.That(snapshot.Nodes.Count, Is.EqualTo(3));
            Assert.That(snapshot.Nodes[0].ContextId, Is.EqualTo(firstRootId));
            Assert.That(snapshot.Nodes[1].ContextId, Is.EqualTo(firstChildId));
            Assert.That(snapshot.Nodes[2].ContextId, Is.EqualTo(secondRootId));
            Assert.That(snapshot.Nodes[1].ParentContextId, Is.EqualTo(firstRootId));
        }

        [Test]
        public void Registry_FrameZeroEnd_RemainsExplicitlyEndedAcrossSnapshotsAndExport()
        {
            var registry = new MobaTraceRegistry();
            var rootId = registry.CreateRootContext(MobaTraceKind.SkillCast, 501);

            Assert.That(registry.EndContext(rootId, TraceLifecycleReason.Completed), Is.True);
            Assert.That(registry.TryGetNodeSnapshot(rootId, out var snapshot), Is.True);
            var typedSnapshot = registry.TryGetSnapshot(rootId);
            var export = registry.ExportRoot(rootId);

            Assert.That(snapshot.CreatedFrame, Is.Zero);
            Assert.That(snapshot.EndedFrame, Is.Zero);
            Assert.That(snapshot.IsEnded, Is.True);
            Assert.That(typedSnapshot.CreatedFrame, Is.Zero);
            Assert.That(typedSnapshot.EndedFrame, Is.Zero);
            Assert.That(typedSnapshot.IsEnded, Is.True);
            Assert.That(export.Nodes[0].CreatedFrame, Is.Zero);
            Assert.That(export.Nodes[0].EndedFrame, Is.Zero);
            Assert.That(export.Nodes[0].IsEnded, Is.True);
        }

        [Test]
        public void QueryTrace_MapsFailedForceEndedAndActiveStates()
        {
            var collector = MakeCollector();
            var registry = new MobaTraceRegistry();
            var rootId = registry.CreateRootContext(MobaTraceKind.SkillCast, 501);
            var failedId = registry.CreateChildContext(rootId, MobaTraceKind.SkillPhase, 502);
            var cancelledId = registry.CreateChildContext(rootId, MobaTraceKind.EffectExecution, 503);
            var activeId = registry.CreateChildContext(rootId, MobaTraceKind.EffectAction, 504);
            registry.EndContext(failedId, TraceLifecycleReason.Failed);
            registry.EndContext(cancelledId, TraceLifecycleReason.Cancelled);
            var store = new MobaBattleDiagnosticTraceReadStore(registry, collector.Store);

            var result = store.QueryTrace(1, rootId);

            Assert.That(result.Items[1].ContextId, Is.EqualTo(failedId));
            Assert.That(result.Items[1].State, Is.EqualTo(BattleDiagnosticTraceNodeState.Failed));
            Assert.That(result.Items[2].ContextId, Is.EqualTo(cancelledId));
            Assert.That(result.Items[2].State, Is.EqualTo(BattleDiagnosticTraceNodeState.ForceEnded));
            Assert.That(result.Items[3].ContextId, Is.EqualTo(activeId));
            Assert.That(result.Items[3].State, Is.EqualTo(BattleDiagnosticTraceNodeState.Active));
        }

        [Test]
        public void QueryTrace_MissingRoot_DistinguishesNotProducedFromEvicted()
        {
            var collector = MakeCollector();
            var registry = new MobaTraceRegistry();
            var store = new MobaBattleDiagnosticTraceReadStore(registry, collector.Store);

            var beforeProduction = store.QueryTrace(1, 999);
            registry.CreateRootContext(MobaTraceKind.SkillCast, 501);
            var afterProduction = store.QueryTrace(2, 999);

            Assert.That(beforeProduction.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotProduced));
            Assert.That(afterProduction.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.Evicted));
        }

        [Test]
        public void LocalSession_WithTraceStore_DeclaresCapabilityAndUsesIndependentRevision()
        {
            var collector = MakeCollector();
            var registry = new MobaTraceRegistry();
            var traceStore = new MobaBattleDiagnosticTraceReadStore(registry, collector.Store);
            var session = new MobaBattleDiagnosticLocalSession(
                collector.Store,
                collector.StateStore,
                traceStore);

            var rootId = registry.CreateRootContext(MobaTraceKind.SkillCast, 501);
            var traceRevision = session.TraceStoreRevision;
            collector.TryCollect(new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.Damage,
                BattleDiagnosticEventChannel.DamageAndHeal));
            var result = session.QueryTrace(1, rootId);

            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.Trace), Is.True);
            Assert.That(traceRevision, Is.GreaterThan(0));
            Assert.That(session.TraceStoreRevision, Is.EqualTo(traceRevision));
            Assert.That(session.EventStoreRevision, Is.EqualTo(1));
            Assert.That(result.Status.StoreRevision, Is.EqualTo(traceRevision));
        }

        [Test]
        public void LocalSession_RejectsTraceStoreFromDifferentScope()
        {
            var collector = MakeCollector();
            var otherCollector = new MobaBattleDiagnosticEventCollector(
                new BattleDiagnosticSessionScope("other", "world", 1),
                8);
            var traceStore = new MobaBattleDiagnosticTraceReadStore(
                new MobaTraceRegistry(),
                otherCollector.Store);

            Assert.Throws<ArgumentException>(() => new MobaBattleDiagnosticLocalSession(
                collector.Store,
                collector.StateStore,
                traceStore));
        }

        [Test]
        public void AttributeWorldServicesModule_ResolvesTraceStoreAndSessionOverSharedScopedRegistry()
        {
            AttributeWorldServicesModule.ClearCache();
            var runtimeAssembly = typeof(MobaBattleDiagnosticTraceReadStore).Assembly;
            var builder = new WorldContainerBuilder()
                .AddModule(new AttributeWorldServicesModule(
                    WorldServiceProfile.Default,
                    new[] { runtimeAssembly },
                    new[] { "AbilityKit.Demo.Moba.Services" }));

            using var container = builder.Build();
            using var scope = container.CreateScope();
            var registry = scope.Resolve<MobaTraceRegistry>();
            var traceStore = scope.Resolve<IBattleDiagnosticTraceReadStore>();
            var session = scope.Resolve<IBattleDiagnosticReadOnlySession>();
            var rootId = registry.CreateRootContext(MobaTraceKind.SkillCast, 501);

            var result = session.QueryTrace(1, rootId);

            Assert.That(traceStore.Revision, Is.EqualTo(registry.Revision));
            Assert.That(session.TraceStoreRevision, Is.EqualTo(registry.Revision));
            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.Trace), Is.True);
            Assert.That(result.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
            Assert.That(result.Items[0].ContextId, Is.EqualTo(rootId));
        }

        private MobaBattleDiagnosticEventCollector MakeCollector()
        {
            return new MobaBattleDiagnosticEventCollector(
                _scope,
                16,
                () => 0,
                () => 0L);
        }
    }
}
