using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Observability;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Observability;
using AbilityKit.Trace;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
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
        public void DefinitionSource_ActionUsesExistingModuleDescriptorsWithoutExecutingActions()
        {
            var modules = new PlanActionModuleRegistry(new IPlanActionModule[] { new GiveDamagePlanActionModule() });
            var descriptor = modules.Descriptors[0];
            var actionId = PlanActionRegisterUtil.GetActionId(descriptor.ActionName).Value;
            var source = new MobaBattleDiagnosticDefinitionCatalogSource(
                new MobaConfigDatabase(), new TriggerPlanJsonDatabase());
            WorldTestInjector.Inject(source, new Dictionary<Type, object>
            {
                [typeof(PlanActionModuleRegistry)] = modules
            });
            var reference = BattleDiagnosticDefinitionReference.Create(BattleDiagnosticDefinitionKind.Action, actionId);
            Assert.That(source.TryResolve(in reference, out var definition), Is.True);
            Assert.That(definition.DisplayName, Is.EqualTo(descriptor.ActionName));
            Assert.That(definition.Metadata[0].StringValue, Is.EqualTo(descriptor.ActionName));
            Assert.That(definition.Metadata[1].StringValue, Is.EqualTo(descriptor.ModuleName));
            var effectReference = BattleDiagnosticDefinitionReference.Create(BattleDiagnosticDefinitionKind.Effect, actionId);
            Assert.That(source.TryResolve(in effectReference, out _), Is.False);
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
            WorldTestInjector.Inject(
                store,
                new Dictionary<Type, object>
                {
                    [typeof(IMobaRuntimeObjectKeyResolver)] =
                        new FixedRuntimeObjectKeyResolver(7, 3, 21, 5)
                });

            var rootId = registry.CreateRootContext(MobaTraceKind.SkillCast, 501, 7, 21);
            frameTime.StepTo(new FrameIndex(11), 0.02f);
            var firstChildId = registry.CreateChildContext(rootId, MobaTraceKind.SkillPhase, 502, 7, 21);
            registry.TrySetSkillPhaseLocation(firstChildId, 501, 7001, "cast.release");
            var secondChildId = registry.CreateChildContext(rootId, MobaTraceKind.EffectExecution, 503, 8, 22);
            registry.TrySetEffectTrigger(secondChildId, 7003);
            registry.TrySetEffectOrigin(secondChildId, MobaTraceKind.AreaStay, 601);
            frameTime.StepTo(new FrameIndex(12), 0.02f);
            var grandChildId = registry.CreateChildContext(firstChildId, MobaTraceKind.EffectAction, 504, 9, 23);
            frameTime.StepTo(new FrameIndex(15), 0.02f);
            registry.EndContext(grandChildId, TraceLifecycleReason.Completed);

            var result = store.QueryTrace(1, rootId);

            Assert.That(
                result.Status.Phase,
                Is.EqualTo(BattleDiagnosticQueryPhase.Ready),
                $"{result.Status.ErrorCode}: {result.Status.Message}");
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
            Assert.That(result.Items[1].SkillId, Is.EqualTo(501));
            Assert.That(result.Items[1].CastFlowId, Is.EqualTo(7001));
            Assert.That(result.Items[1].PhaseId, Is.EqualTo("cast.release"));
            Assert.That(result.Items[2].State, Is.EqualTo(BattleDiagnosticTraceNodeState.Ended));
            Assert.That(result.Items[2].EndReason, Is.EqualTo(nameof(TraceLifecycleReason.Completed)));
            Assert.That(result.Items[2].ActorId, Is.EqualTo(9));
            Assert.That(result.Items[2].SourceActorId, Is.EqualTo(9));
            Assert.That(result.Items[2].TargetActorId, Is.EqualTo(23));
            Assert.That(result.Items[2].ConfigId, Is.EqualTo(504));
            Assert.That(result.Items[3].State, Is.EqualTo(BattleDiagnosticTraceNodeState.Active));
            Assert.That(result.Items[3].EndFrame, Is.EqualTo(BattleDiagnosticFrames.Invalid));
            Assert.That(result.Items[3].TriggerId, Is.EqualTo(7003));
            Assert.That(result.Items[0].RootContext.ContextId, Is.EqualTo(rootId));
            Assert.That(result.Items[2].ParentContext.ContextId, Is.EqualTo(firstChildId));
            Assert.That(result.Items[0].SourceObject.Kind,
                Is.EqualTo(BattleDiagnosticRuntimeObjectKind.Actor));
            Assert.That(result.Items[0].SourceActorGeneration, Is.EqualTo(3));
            Assert.That(result.Items[0].TargetActorGeneration, Is.EqualTo(5));
            Assert.That(result.Items[0].Definition.Kind,
                Is.EqualTo(BattleDiagnosticDefinitionKind.Skill));
            Assert.That(result.Items[2].Definition.Kind,
                Is.EqualTo(BattleDiagnosticDefinitionKind.Action));
            Assert.That(result.Items[3].TriggerDefinition.Kind,
                Is.EqualTo(BattleDiagnosticDefinitionKind.Trigger));
            Assert.That(result.Items[3].HasOrigin, Is.True);
            Assert.That(result.Items[3].OriginKind, Is.EqualTo((int)MobaTraceKind.AreaStay));
            Assert.That(result.Items[3].OriginDefinition,
                Is.EqualTo(BattleDiagnosticDefinitionReference.Create(BattleDiagnosticDefinitionKind.Area, 601)));
        }

        [Test]
        public void PredictionRetraction_RemovesCurrentTreeAndKeepsHistoricalEvidenceWithMarker()
        {
            var collector = MakeCollector();
            using var registry = new MobaTraceRegistry();
            registry.AttachDiagnosticCollector(collector);
            var root = registry.CreateRootContext(MobaTraceKind.SkillCast, 501);
            var boundary = registry.NextContextId;
            var predictedChild = registry.CreateChildContext(root, MobaTraceKind.EffectExecution, 502);
            var predictedRoot = registry.CreateRootContext(MobaTraceKind.SkillCast, 503);
            var store = new MobaBattleDiagnosticTraceReadStore(registry, collector.Store);
            var before = store.QueryTrace(1, root);
            Assert.That(before.Items.Count, Is.EqualTo(2));
            var historicalCount = collector.Store.Count;

            Assert.That(registry.RetractPrediction(boundary), Is.EqualTo(2));

            var after = store.QueryTrace(2, root);
            Assert.That(after.Items.Count, Is.EqualTo(1));
            Assert.That(after.Items[0].ContextId, Is.EqualTo(root));
            Assert.That(after.Status.StoreRevision, Is.GreaterThan(before.Status.StoreRevision));
            Assert.That(store.QueryTrace(3, predictedRoot).Items, Is.Empty);
            var snapshot = store.CaptureTraceSnapshot();
            Assert.That(snapshot.Nodes.Count, Is.EqualTo(1));
            Assert.That(snapshot.Nodes[0].ContextId, Is.EqualTo(root));
            Assert.That(registry.Contains(predictedChild), Is.False);
            Assert.That(collector.Store.Count, Is.EqualTo(historicalCount + 1));
            var events = collector.Store.Query(new BattleDiagnosticEventQuery(
                1, BattleDiagnosticFilter.Default, new BattleDiagnosticPageRequest(0, 0, 100))).Items;
            var markerFound = false;
            foreach (var diagnosticEvent in events)
            {
                if (diagnosticEvent.Kind != BattleDiagnosticEventKind.TracePredictionRetracted) continue;
                markerFound = true;
                Assert.That(diagnosticEvent.ContextId, Is.Zero);
                Assert.That(diagnosticEvent.RootContextId, Is.Zero);
                Assert.That(diagnosticEvent.Summary, Does.Contain($"[{boundary}, {registry.NextContextId})"));
            }
            Assert.That(markerFound, Is.True);
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
        public void QueryTraceRoots_PrioritizesIssuesThenActiveAndSupportsPaging()
        {
            var collector = MakeCollector();
            var registry = new MobaTraceRegistry();
            var issueRoot = registry.CreateRootContext(MobaTraceKind.SkillCast, 501);
            var failedAction = registry.CreateChildContext(
                issueRoot,
                MobaTraceKind.EffectAction,
                601);
            registry.EndContext(failedAction, TraceLifecycleReason.Failed);
            registry.EndContext(issueRoot, TraceLifecycleReason.Completed);

            var activeRoot = registry.CreateRootContext(MobaTraceKind.SkillCast, 502);
            registry.CreateChildContext(activeRoot, MobaTraceKind.SkillPhase, 602);

            var completedRoot = registry.CreateRootContext(MobaTraceKind.EffectExecution, 503);
            registry.EndContext(completedRoot, TraceLifecycleReason.Completed);

            var store = new MobaBattleDiagnosticTraceReadStore(registry, collector.Store);
            var result = store.QueryTraceRoots(new BattleDiagnosticTraceRootQuery(
                1,
                new BattleDiagnosticPageRequest(0, 0, 2)));

            Assert.That(result.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
            Assert.That(result.Status.HasMore, Is.True);
            Assert.That(result.Items.Count, Is.EqualTo(2));
            Assert.That(result.Items[0].RootContextId, Is.EqualTo(issueRoot));
            Assert.That(result.Items[0].IssueCount, Is.EqualTo(1));
            Assert.That(result.Items[0].ActionCount, Is.EqualTo(1));
            Assert.That(result.Items[1].RootContextId, Is.EqualTo(activeRoot));
            Assert.That(result.Items[1].ActiveCount, Is.EqualTo(2));
            Assert.That(result.Items[1].NodeCount, Is.EqualTo(2));
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

            Assert.That(
                result.Status.Phase,
                Is.EqualTo(BattleDiagnosticQueryPhase.Ready),
                $"{result.Status.ErrorCode}: {result.Status.Message}");
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

        private sealed class FixedRuntimeObjectKeyResolver : IMobaRuntimeObjectKeyResolver
        {
            private readonly long _sourceActorId;
            private readonly int _sourceGeneration;
            private readonly long _targetActorId;
            private readonly int _targetGeneration;

            public FixedRuntimeObjectKeyResolver(
                long sourceActorId,
                int sourceGeneration,
                long targetActorId,
                int targetGeneration)
            {
                _sourceActorId = sourceActorId;
                _sourceGeneration = sourceGeneration;
                _targetActorId = targetActorId;
                _targetGeneration = targetGeneration;
            }

            public bool TryResolve(
                MobaRuntimeObjectKind kind,
                long runtimeId,
                int frame,
                out RuntimeObjectKey key)
            {
                if (kind == MobaRuntimeObjectKind.Actor && runtimeId == _sourceActorId)
                {
                    key = new RuntimeObjectKey(runtimeId, _sourceGeneration);
                    return true;
                }
                if (kind == MobaRuntimeObjectKind.Actor && runtimeId == _targetActorId)
                {
                    key = new RuntimeObjectKey(runtimeId, _targetGeneration);
                    return true;
                }

                key = default;
                return false;
            }
        }
    }
}
