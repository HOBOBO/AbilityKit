using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Attributes.Core;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.ECS;
using AbilityKit.Effect;
using AbilityKit.GameplayTags;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaBattleDiagnosticStateSamplerTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 1);
        }

        [TestCase(EntityMainType.Unit, UnitSubType.Hero, BattleDiagnosticActorKind.Hero)]
        [TestCase(EntityMainType.Unit, UnitSubType.Minion, BattleDiagnosticActorKind.Minion)]
        [TestCase(EntityMainType.Unit, UnitSubType.Neutral, BattleDiagnosticActorKind.Monster)]
        [TestCase(EntityMainType.Unit, UnitSubType.Boss, BattleDiagnosticActorKind.Monster)]
        [TestCase(EntityMainType.Unit, UnitSubType.Tower, BattleDiagnosticActorKind.Building)]
        [TestCase(EntityMainType.Unit, UnitSubType.Base, BattleDiagnosticActorKind.Building)]
        [TestCase(EntityMainType.Projectile, UnitSubType.None, BattleDiagnosticActorKind.Projectile)]
        [TestCase(EntityMainType.Summon, UnitSubType.None, BattleDiagnosticActorKind.Summon)]
        [TestCase(EntityMainType.SceneObject, UnitSubType.None, BattleDiagnosticActorKind.Area)]
        [TestCase(EntityMainType.None, UnitSubType.None, BattleDiagnosticActorKind.Unknown)]
        public void ResolveActorKind_MapsAllCombinations(
            EntityMainType mainType,
            UnitSubType unitSubType,
            BattleDiagnosticActorKind expected)
        {
            var kind = MobaBattleDiagnosticStateSampler.ResolveActorKind(mainType, unitSubType);

            Assert.That(kind, Is.EqualTo(expected));
        }

        [Test]
        public void Sample_EmptyRegistry_RecordsSuccessfulFrame()
        {
            var store = new BattleDiagnosticStateStore(_scope);
            var sampler = new MobaBattleDiagnosticStateSampler(
                new MobaActorRegistry(),
                store,
                () => 27,
                () => 100L);

            Assert.That(sampler.Sample(), Is.True);
            Assert.That(sampler.LastSuccessfulSampleFrame, Is.EqualTo(27));
            Assert.That(sampler.SampleFailureCount, Is.Zero);
            Assert.That(sampler.LastSampleError, Is.Empty);
            Assert.That(store.SnapshotFrame, Is.EqualTo(27));
        }

        [Test]
        public void Sample_ProviderException_RecordsBoundedFailureEvidence()
        {
            var store = new BattleDiagnosticStateStore(_scope);
            var sampler = new MobaBattleDiagnosticStateSampler(
                new MobaActorRegistry(),
                store,
                () => throw new InvalidOperationException("sample frame failed"),
                () => 100L);

            Assert.DoesNotThrow(() => sampler.Sample());
            Assert.That(sampler.Sample(), Is.False);
            Assert.That(sampler.LastSuccessfulSampleFrame, Is.EqualTo(BattleDiagnosticFrames.Invalid));
            Assert.That(sampler.SampleFailureCount, Is.EqualTo(2));
            StringAssert.Contains("sample frame failed", sampler.LastSampleError);
        }

        [Test]
        public void TrySampleActor_NullEntity_ReturnsFalse()
        {
            var ok = MobaBattleDiagnosticStateSampler.TrySampleActor(
                _scope, 0, 1, null, out var summary);

            Assert.That(ok, Is.False);
            Assert.That(summary, Is.EqualTo(default(BattleDiagnosticActorSummary)));
        }

        [Test]
        public void TrySampleActor_ZeroActorId_ReturnsFalse()
        {
            var ok = MobaBattleDiagnosticStateSampler.TrySampleActor(
                _scope, 0, 0, new object(), out var summary);

            Assert.That(ok, Is.False);
        }

        [Test]
        public void TrySampleActor_WrongType_ReturnsFalse()
        {
            var ok = MobaBattleDiagnosticStateSampler.TrySampleActor(
                _scope, 0, 1, new object(), out var summary);

            Assert.That(ok, Is.False);
        }

        [Test]
        public void TrySampleActorBuffs_ActorWithoutBuffs_ReturnsEmptySuccess()
        {
            var context = new ActorContext();
            var entity = context.CreateEntity();
            var buffs = new List<BattleDiagnosticActorBuff>();

            var ok = MobaBattleDiagnosticStateSampler.TrySampleActorBuffs(
                _scope, 3, 10, entity, buffs);

            Assert.That(ok, Is.True);
            Assert.That(buffs, Is.Empty);
        }

        [Test]
        public void TrySampleActorBuffs_ProjectsRuntimeFieldsAndNormalizesInvalidValues()
        {
            var context = new ActorContext();
            var entity = context.CreateEntity();
            entity.AddBuffs(new List<BuffRuntime>
            {
                null,
                new BuffRuntime { BuffId = 0 },
                new BuffRuntime
                {
                    BuffId = 1001,
                    SourceId = 20,
                    StackCount = 2,
                    Remaining = float.PositiveInfinity,
                    IntervalRemainingSeconds = 1.5f,
                    SourceContextId = 30,
                    RuntimeContextId = 40,
                    RuntimeContextVersion = 3,
                    ModifierBindings = new List<AbilityKit.Demo.Moba.Components.BuffModifierBinding>
                    {
                        new AbilityKit.Demo.Moba.Components.BuffModifierBinding()
                    }
                }
            });
            var buffs = new List<BattleDiagnosticActorBuff>();

            var ok = MobaBattleDiagnosticStateSampler.TrySampleActorBuffs(
                _scope, 3, 10, entity, buffs);

            Assert.That(ok, Is.True);
            Assert.That(buffs.Count, Is.EqualTo(1));
            Assert.That(buffs[0].ActorId, Is.EqualTo(10));
            Assert.That(buffs[0].BuffId, Is.EqualTo(1001));
            Assert.That(buffs[0].SourceActorId, Is.EqualTo(20));
            Assert.That(buffs[0].StackCount, Is.EqualTo(2));
            Assert.That(buffs[0].RemainingSeconds, Is.Zero);
            Assert.That(buffs[0].IntervalRemainingSeconds, Is.EqualTo(1.5f));
            Assert.That(buffs[0].SourceContextId, Is.EqualTo(30));
            Assert.That(buffs[0].RuntimeContextId, Is.EqualTo(40));
            Assert.That(buffs[0].RuntimeContextVersion, Is.EqualTo(3));
            Assert.That(buffs[0].ModifierBindingCount, Is.EqualTo(1));
        }

        [Test]
        public void TrySampleActorEffects_ProjectsActiveEffectsAndTimingSemantics()
        {
            var unit = new TestUnitFacade(10);
            var time = new FrameTime();
            time.Reset(new FrameIndex(0), 0f, 0.5f);
            var context = new EffectExecutionContext(
                null,
                time,
                unit,
                unit,
                0,
                unit,
                null);
            unit.Effects.Apply(new GameplayEffectSpec(
                EffectDurationPolicy.Duration,
                5f,
                2f,
                default,
                null,
                Array.Empty<IEffectComponent>(),
                true), in context);
            unit.Effects.Apply(new GameplayEffectSpec(
                EffectDurationPolicy.Infinite,
                0f,
                0f,
                default,
                null,
                Array.Empty<IEffectComponent>()), in context);
            time.StepTo(new FrameIndex(1), 0.5f);
            unit.Effects.Step(in context);
            var effects = new List<BattleDiagnosticActorEffect>();

            var ok = MobaBattleDiagnosticStateSampler.TrySampleActorEffects(
                _scope, 3, 10, unit, effects);

            Assert.That(ok, Is.True);
            Assert.That(effects.Count, Is.EqualTo(2));
            Assert.That(effects[0].InstanceId, Is.EqualTo(1));
            Assert.That(effects[0].DurationPolicy,
                Is.EqualTo(BattleDiagnosticEffectDurationPolicy.Duration));
            Assert.That(effects[0].ElapsedSeconds, Is.EqualTo(0.5f));
            Assert.That(effects[0].RemainingSeconds, Is.EqualTo(4.5f));
            Assert.That(effects[0].HasRemainingTime, Is.True);
            Assert.That(effects[0].NextTickInSeconds, Is.EqualTo(1.5f));
            Assert.That(effects[0].HasPeriodicTick, Is.True);
            Assert.That(effects[0].ExecutePeriodicOnApply, Is.True);
            Assert.That(effects[1].InstanceId, Is.EqualTo(2));
            Assert.That(effects[1].DurationPolicy,
                Is.EqualTo(BattleDiagnosticEffectDurationPolicy.Infinite));
            Assert.That(effects[1].RemainingSeconds, Is.Zero);
            Assert.That(effects[1].HasRemainingTime, Is.False);
            Assert.That(effects[1].NextTickInSeconds, Is.Zero);
            Assert.That(effects[1].HasPeriodicTick, Is.False);
        }

        [Test]
        public void AttributeWorldServicesModule_ResolvesSamplerWithScopedLifetime()
        {
            AttributeWorldServicesModule.ClearCache();
            var runtimeAssembly = typeof(MobaBattleDiagnosticStateSampler).Assembly;
            var builder = new WorldContainerBuilder()
                .AddModule(new AttributeWorldServicesModule(
                    WorldServiceProfile.Default,
                    new[] { runtimeAssembly },
                    new[] { "AbilityKit.Demo.Moba.Services" }));

            using var container = builder.Build();
            Assert.That(container.IsRegistered(typeof(MobaBattleDiagnosticStateSampler)), Is.True);

            using var firstScope = container.CreateScope();
            using var secondScope = container.CreateScope();
            var first = firstScope.Resolve<MobaBattleDiagnosticStateSampler>();
            var firstAgain = firstScope.Resolve<MobaBattleDiagnosticStateSampler>();
            var second = secondScope.Resolve<MobaBattleDiagnosticStateSampler>();

            Assert.That(first, Is.Not.Null);
            Assert.That(firstAgain, Is.SameAs(first));
            Assert.That(second, Is.Not.SameAs(first));
        }

        [Test]
        public void AttributeWorldServicesModule_ResolvesCollectorPortsAndActorStoresWithoutCycle()
        {
            AttributeWorldServicesModule.ClearCache();
            var runtimeAssembly = typeof(MobaBattleDiagnosticEventCollector).Assembly;
            var builder = new WorldContainerBuilder()
                .AddModule(new AttributeWorldServicesModule(
                    WorldServiceProfile.Default,
                    new[] { runtimeAssembly },
                    new[] { "AbilityKit.Demo.Moba.Services" }));

            using var container = builder.Build();
            using var scope = container.CreateScope();
            var sink = scope.Resolve<IMobaBattleDiagnosticEventSink>();
            var collector = scope.Resolve<MobaBattleDiagnosticEventCollector>();
            var attributeStore = scope.Resolve<IBattleDiagnosticActorAttributeStore>();
            var buffStore = scope.Resolve<IBattleDiagnosticActorBuffStore>();
            var tagStore = scope.Resolve<IBattleDiagnosticActorTagStore>();
            var effectStore = scope.Resolve<IBattleDiagnosticActorEffectStore>();

            Assert.That(sink, Is.Not.Null);
            Assert.That(attributeStore.Scope, Is.EqualTo(collector.Scope));
            Assert.That(buffStore.Scope, Is.EqualTo(collector.Scope));
            Assert.That(tagStore.Scope, Is.EqualTo(collector.Scope));
            Assert.That(effectStore.Scope, Is.EqualTo(collector.Scope));
        }

        [Test]
        public void AttributeWorldServicesModule_ResolvesNarrowPortsOverOneCollector()
        {
            AttributeWorldServicesModule.ClearCache();
            var runtimeAssembly = typeof(MobaBattleDiagnosticEventCollector).Assembly;
            var builder = new WorldContainerBuilder()
                .AddModule(new AttributeWorldServicesModule(
                    WorldServiceProfile.Default,
                    new[] { runtimeAssembly },
                    new[] { "AbilityKit.Demo.Moba.Services" }));

            using var container = builder.Build();
            using var scope = container.CreateScope();
            var collector = scope.Resolve<MobaBattleDiagnosticEventCollector>();
            var ports = scope.Resolve<MobaBattleDiagnosticCollectorPorts>();
            var sink = scope.Resolve<IMobaBattleDiagnosticEventSink>();
            var control = scope.Resolve<IMobaBattleDiagnosticCaptureControl>();
            var snapshotCapture = scope.Resolve<IMobaBattleDiagnosticSnapshotCapture>();
            var eventStore = scope.Resolve<IBattleDiagnosticEventReadStore>();
            var stateStore = scope.Resolve<IBattleDiagnosticStateStore>();
            var stateReadStore = scope.Resolve<IBattleDiagnosticStateReadStore>();
            var attributeStore = scope.Resolve<IBattleDiagnosticActorAttributeStore>();
            var attributeReadStore = scope.Resolve<IBattleDiagnosticActorAttributeReadStore>();
            var buffStore = scope.Resolve<IBattleDiagnosticActorBuffStore>();
            var buffReadStore = scope.Resolve<IBattleDiagnosticActorBuffReadStore>();
            var tagStore = scope.Resolve<IBattleDiagnosticActorTagStore>();
            var tagReadStore = scope.Resolve<IBattleDiagnosticActorTagReadStore>();
            var session = scope.Resolve<IBattleDiagnosticReadOnlySession>();
            var draft = new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.Damage,
                BattleDiagnosticEventChannel.DamageAndHeal);

            Assert.That(sink, Is.SameAs(ports));
            Assert.That(control, Is.SameAs(ports));
            Assert.That(eventStore, Is.SameAs(ports));
            Assert.That(stateStore, Is.SameAs(ports));
            Assert.That(stateReadStore, Is.SameAs(ports));
            Assert.That(attributeReadStore, Is.SameAs(attributeStore));
            Assert.That(buffReadStore, Is.SameAs(buffStore));
            Assert.That(tagReadStore, Is.SameAs(tagStore));

            Assert.That(sink.TryCollect(in draft), Is.True);
            Assert.That(eventStore.Revision, Is.EqualTo(collector.Store.Revision));
            Assert.That(control.LastSequence, Is.EqualTo(collector.LastSequence));

            var world = new BattleDiagnosticWorldSummary(
                collector.Scope,
                1,
                1L,
                0,
                0,
                0);
            Assert.That(stateStore.TryReplaceSnapshot(
                world,
                new BattleDiagnosticActorSummary[0]), Is.True);
            Assert.That(stateReadStore.Revision, Is.EqualTo(collector.StateStore.Revision));
            Assert.That(attributeStore.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                new BattleDiagnosticActorAttribute[0],
                new BattleDiagnosticActorAttributeModifier[0]), Is.True);
            Assert.That(attributeReadStore.SnapshotFrame, Is.EqualTo(1));
            Assert.That(buffStore.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                new BattleDiagnosticActorBuff[0]), Is.True);
            Assert.That(buffReadStore.SnapshotFrame, Is.EqualTo(1));
            Assert.That(session.ActorBuffStoreRevision, Is.EqualTo(buffReadStore.Revision));
            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorBuffs), Is.True);
            Assert.That(session.QueryActorBuffs(1, 1, 10).Status.Phase,
                Is.EqualTo(BattleDiagnosticQueryPhase.Empty));
            Assert.That(tagStore.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                new BattleDiagnosticActorTag[0]), Is.True);
            Assert.That(tagReadStore.SnapshotFrame, Is.EqualTo(1));
            Assert.That(session.ActorTagStoreRevision, Is.EqualTo(tagReadStore.Revision));
            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorTags), Is.True);
            Assert.That(session.QueryActorTags(1, 1, 10).Status.Phase,
                Is.EqualTo(BattleDiagnosticQueryPhase.Empty));

            var effectStore = scope.Resolve<IBattleDiagnosticActorEffectStore>();
            var effectReadStore = scope.Resolve<IBattleDiagnosticActorEffectReadStore>();
            Assert.That(effectReadStore, Is.SameAs(effectStore));
            Assert.That(effectStore.TryReplaceSnapshot(
                1,
                new long[] { 10 },
                new BattleDiagnosticActorEffect[0]), Is.True);
            Assert.That(effectReadStore.SnapshotFrame, Is.EqualTo(1));
            Assert.That(session.ActorEffectStoreRevision, Is.EqualTo(effectReadStore.Revision));
            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.ActorEffects), Is.True);
            Assert.That(session.QueryActorEffects(1, 1, 10).Status.Phase,
                Is.EqualTo(BattleDiagnosticQueryPhase.Empty));

            var snapshot = snapshotCapture.CaptureSnapshot();
            Assert.That(snapshot.SessionInfo.Scope, Is.EqualTo(collector.Scope));
            Assert.That(snapshot.Events.Revision, Is.EqualTo(eventStore.Revision));
            Assert.That(snapshot.Events.Events.Count, Is.EqualTo(1));
            Assert.That(snapshot.State.Revision, Is.EqualTo(stateReadStore.Revision));
            Assert.That(snapshot.State.Frame, Is.EqualTo(1));
            Assert.That(snapshot.Attributes.Revision, Is.EqualTo(attributeReadStore.Revision));
            Assert.That(snapshot.Buffs.Revision, Is.EqualTo(buffReadStore.Revision));
            Assert.That(snapshot.Tags.Revision, Is.EqualTo(tagReadStore.Revision));
            Assert.That(snapshot.Effects.Revision, Is.EqualTo(effectReadStore.Revision));
            Assert.That(snapshot.LatestStateFramesAligned, Is.True);
            Assert.That(snapshot.Trace.IsStable, Is.True);

            control.SetFrozen(true);
            Assert.That(collector.Store.IsFrozen, Is.True);
            Assert.That(collector.StateStore.IsFrozen, Is.True);
            Assert.That(attributeStore.IsFrozen, Is.True);
            Assert.That(buffStore.IsFrozen, Is.True);
            Assert.That(tagStore.IsFrozen, Is.True);
            Assert.That(effectStore.IsFrozen, Is.True);

            control.SetFrozen(false);
            control.Clear();
            Assert.That(snapshot.Events.Events.Count, Is.EqualTo(1));
            Assert.That(snapshot.State.Frame, Is.EqualTo(1));
            Assert.That(snapshot.Attributes.Frame, Is.EqualTo(1));
            Assert.That(snapshot.Buffs.Frame, Is.EqualTo(1));
            Assert.That(snapshot.Tags.Frame, Is.EqualTo(1));
            Assert.That(snapshot.Effects.Frame, Is.EqualTo(1));
            Assert.That(attributeReadStore.SnapshotFrame,
                Is.EqualTo(BattleDiagnosticFrames.Invalid));
            Assert.That(buffReadStore.SnapshotFrame,
                Is.EqualTo(BattleDiagnosticFrames.Invalid));
            Assert.That(tagReadStore.SnapshotFrame,
                Is.EqualTo(BattleDiagnosticFrames.Invalid));
            Assert.That(effectReadStore.SnapshotFrame,
                Is.EqualTo(BattleDiagnosticFrames.Invalid));
        }

        private sealed class TestUnitFacade : IUnitFacade
        {
            public TestUnitFacade(int actorId)
            {
                Id = new EcsEntityId(actorId);
            }

            public EcsEntityId Id { get; }
            public GameplayTagContainer Tags { get; } = new GameplayTagContainer();
            public AttributeContext Attributes { get; } = new AttributeContext();
            public EffectContainer Effects { get; } = new EffectContainer();
        }
    }

    public sealed class MobaBattleDiagnosticLocalSessionTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 1);
        }

        [Test]
        public void SessionInfo_DeclaresOnlyImplementedReadCapabilities()
        {
            var collector = MakeCollector();
            var session = new MobaBattleDiagnosticLocalSession(collector);

            Assert.That(session.SessionInfo.ConnectionState,
                Is.EqualTo(BattleDiagnosticConnectionState.Connected));
            Assert.That(session.SessionInfo.CaptureState,
                Is.EqualTo(BattleDiagnosticCaptureState.Capturing));
            Assert.That(session.SessionInfo.Capabilities, Is.EqualTo(
                BattleDiagnosticCapabilities.WorldState |
                BattleDiagnosticCapabilities.ActorState |
                BattleDiagnosticCapabilities.Events));
            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.SkillRuntime), Is.False);
            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.FreezeCapture), Is.False);
            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.Clear), Is.False);
            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.SelfMetrics), Is.False);
            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.Trace), Is.False);
            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.PinTrace), Is.False);
            Assert.That(session.SessionInfo.Supports(BattleDiagnosticCapabilities.Export), Is.False);
        }

        [Test]
        public void NarrowStoreConstructor_PreservesLocalSessionQueries()
        {
            var collector = MakeCollector();
            var session = new MobaBattleDiagnosticLocalSession(
                collector.Store,
                collector.StateStore);
            collector.TryCollect(new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.Damage,
                BattleDiagnosticEventChannel.DamageAndHeal));

            var result = session.QueryEvents(new BattleDiagnosticEventQuery(
                1,
                BattleDiagnosticFilter.Default,
                new BattleDiagnosticPageRequest(0, 0, 10)));

            Assert.That(result.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
            Assert.That(result.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void QueryWorld_BeforeSampling_ReturnsNotProduced()
        {
            var collector = MakeCollector();
            var session = new MobaBattleDiagnosticLocalSession(collector);

            var result = session.QueryWorld(1, 0);

            Assert.That(result.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Unavailable));
            Assert.That(result.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.NotProduced));
        }

        [Test]
        public void QueryWorld_AfterManualSampling_ReturnsReady()
        {
            var collector = MakeCollector();
            var session = new MobaBattleDiagnosticLocalSession(collector);
            var world = new BattleDiagnosticWorldSummary(_scope, 5, 1000L, 0, 0, 0);
            collector.StateStore.TryReplaceSnapshot(world, new BattleDiagnosticActorSummary[0]);

            var result = session.QueryWorld(1, 5);

            Assert.That(result.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(result.Items[0].ActorCount, Is.Zero);
        }

        [Test]
        public void QueryActors_AfterManualSampling_ReturnsReady()
        {
            var collector = MakeCollector();
            var session = new MobaBattleDiagnosticLocalSession(collector);
            var actors = new List<BattleDiagnosticActorSummary>
            {
                new BattleDiagnosticActorSummary(
                    _scope, 5, 1, BattleDiagnosticActorKind.Hero, 100, 1, 1, 2, 3, 80, 100, true),
                new BattleDiagnosticActorSummary(
                    _scope, 5, 2, BattleDiagnosticActorKind.Minion, 200, 2, 4, 5, 6, 30, 50, true)
            };
            collector.StateStore.TryReplaceSnapshot(
                new BattleDiagnosticWorldSummary(_scope, 5, 1000L, actors.Count, 0, 0),
                actors);

            var result = session.QueryActors(1, 0);

            Assert.That(result.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
            Assert.That(result.Items.Count, Is.EqualTo(2));
        }

        [Test]
        public void QueryEvents_RoutesToEventRingStore()
        {
            var collector = MakeCollector();
            var session = new MobaBattleDiagnosticLocalSession(collector);

            // Submit one event
            collector.TryCollect(new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.Damage,
                BattleDiagnosticEventChannel.DamageAndHeal));

            var query = new BattleDiagnosticEventQuery(
                1,
                BattleDiagnosticFilter.Default,
                new BattleDiagnosticPageRequest(collector.Store.Revision, 0, 10));

            var result = session.QueryEvents(query);

            Assert.That(result.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
            Assert.That(result.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void QueryTrace_WithoutTraceStore_ReturnsUnsupported()
        {
            var collector = MakeCollector();
            var session = new MobaBattleDiagnosticLocalSession(collector);

            var result = session.QueryTrace(1, 100);

            Assert.That(result.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Unavailable));
            Assert.That(result.Status.Availability,
                Is.EqualTo(BattleDiagnosticDataAvailability.Unsupported));
            Assert.That(session.TraceStoreRevision, Is.Zero);
        }

        [Test]
        public void Revisions_AreIndependentAndStoreRevisionAliasesEvents()
        {
            var collector = MakeCollector();
            var session = new MobaBattleDiagnosticLocalSession(collector);

            collector.StateStore.TryReplaceSnapshot(
                new BattleDiagnosticWorldSummary(_scope, 5, 1000L, 0, 0, 0),
                new BattleDiagnosticActorSummary[0]);

            Assert.That(session.StateStoreRevision, Is.EqualTo(1));
            Assert.That(session.EventStoreRevision, Is.Zero);
            Assert.That(session.StoreRevision, Is.EqualTo(session.EventStoreRevision));

            collector.TryCollect(new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.Damage,
                BattleDiagnosticEventChannel.DamageAndHeal));

            Assert.That(session.EventStoreRevision, Is.EqualTo(1));
            Assert.That(session.StateStoreRevision, Is.EqualTo(1));
            Assert.That(session.StoreRevision, Is.EqualTo(session.EventStoreRevision));
        }

        [Test]
        public void QueryState_NonLatestFrameReturnsNotCapturedForWorldAndActors()
        {
            var collector = MakeCollector();
            var session = new MobaBattleDiagnosticLocalSession(collector);
            collector.StateStore.TryReplaceSnapshot(
                new BattleDiagnosticWorldSummary(_scope, 5, 1000L, 0, 0, 0),
                new BattleDiagnosticActorSummary[0]);

            var world = session.QueryWorld(1, 4);
            var actors = session.QueryActors(2, 4);

            Assert.That(world.Status.Availability, Is.EqualTo(BattleDiagnosticDataAvailability.NotCaptured));
            Assert.That(actors.Status.Availability, Is.EqualTo(BattleDiagnosticDataAvailability.NotCaptured));
            StringAssert.Contains("latest-only snapshot is frame 5", world.Status.Message);
            StringAssert.Contains("latest-only snapshot is frame 5", actors.Status.Message);
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
