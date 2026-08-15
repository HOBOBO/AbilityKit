using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Attributes.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Moba.StateSync;
using NUnit.Framework;

namespace AbilityKit.Game.Tests
{
    public sealed class MobaSynchronizationCompositionTests
    {
        [Test]
        public void AuthoritativeStateHash_IsStableAndSharedBySnapshotService()
        {
            var context = new ActorContext();
            try
            {
                var first = CreateActor(context, new Vec3(1f, 2f, 3f), 90f);
                var second = CreateActor(context, new Vec3(4f, 5f, 6f), 75f);
                var forwardRegistry = new MobaActorRegistry();
                forwardRegistry.Register(20, second);
                forwardRegistry.Register(10, first);
                var reverseRegistry = new MobaActorRegistry();
                reverseRegistry.Register(10, first);
                reverseRegistry.Register(20, second);
                var calculator = new MobaAuthoritativeStateHashCalculator();

                var expected = calculator.Compute(true, forwardRegistry);

                Assert.That(calculator.Compute(true, reverseRegistry), Is.EqualTo(expected),
                    "Entity registration order must not affect the state hash.");
                Assert.That(calculator.Compute(false, forwardRegistry), Is.Not.EqualTo(expected),
                    "The run-gate state is part of the authoritative projection.");

                var phase = new MobaLogicWorldRunGateService();
                phase.SetInGame("state hash test");
                var service = new MobaStateHashSnapshotService(phase, forwardRegistry);
                Assert.That(service.TryGetSnapshot(new FrameIndex(10), out var snapshot), Is.True);
                var payload = MobaStateHashSnapshotCodec.Deserialize(snapshot.Payload);
                Assert.That(payload.Hash, Is.EqualTo(expected),
                    "Snapshot production and prediction must use the same calculator.");

                first.ReplaceTransform(new Transform3(new Vec3(9f, 2f, 3f), Quat.Identity, Vec3.One));
                Assert.That(calculator.Compute(true, forwardRegistry), Is.Not.EqualTo(expected),
                    "Transform changes must be visible to reconciliation.");

                first.ReplaceTransform(new Transform3(new Vec3(1f, 2f, 3f), Quat.Identity, Vec3.One));
                // 真实血量在 ResourceContainer（Q32.32），HP 变化必须能被对账哈希感知。
                first.resourceContainer.Value.Map[ResourceType.Hp].Current = Fixed64.FromInt32(89);
                Assert.That(calculator.Compute(true, forwardRegistry), Is.Not.EqualTo(expected),
                    "HP changes must be visible to reconciliation.");
            }
            finally
            {
                context.DestroyAllEntities();
            }
        }

        [Test]
        public void RollbackRegistryBuilder_RegistersCompleteAvailableStateSet()
        {
            var services = new TestWorldResolver();
            services.Add<IFrameTime>(new FrameTime());
            services.Add(new MobaActorRegistry());
            services.Add(new RollbackWorldRandom());
            services.Add(new PassiveSkillTriggerEventRollbackLog());
            using var world = new TestWorld(services);

            var registry = MobaRollbackRegistryBuilder.Create(world);
            var wrapperRegistry = new MobaRollbackRegistryFactory().Create(world);
            var expectedKeys = new[]
            {
                MobaActorTransformRollbackProvider.DefaultKey,
                MobaActorHpRollbackProvider.DefaultKey,
                MobaBuffTimerRollbackProvider.DefaultKey,
                MobaSkillCooldownRollbackProvider.DefaultKey,
                RollbackWorldRandom.DefaultKey,
                PassiveSkillTriggerEventRollbackLog.DefaultKey,
                FrameTimeRollbackStateProvider.DefaultKey
            }.OrderBy(key => key).ToArray();

            CollectionAssert.AreEqual(expectedKeys, registry.Providers.Select(provider => provider.Key).ToArray());
            CollectionAssert.AreEqual(expectedKeys, wrapperRegistry.Providers.Select(provider => provider.Key).ToArray(),
                "BattleLogicSession and RemoteDriven sessions must share the same registry definition.");
        }

        [Test]
        public void PredictionReconciliationReporter_ReportsEachMismatchOnlyOnce()
        {
            var reporter = new MobaPredictionReconciliationReporter();
            var sample = CreatePredictionSample(
                totalMismatchCount: 1,
                totalRollbackCount: 1,
                isReplaying: true,
                clientFrame: 18);

            var mismatch = reporter.Observe(in sample);
            var replayProgress = reporter.Observe(in sample);

            Assert.That(mismatch.Reason, Is.EqualTo(SyncReconciliationReason.AuthoritativeHashMismatch));
            Assert.That(mismatch.RecoveryState, Is.EqualTo(SyncRecoveryState.CatchUp));
            Assert.That(mismatch.ClientFrame, Is.EqualTo(18));
            Assert.That(mismatch.AuthoritativeFrame, Is.EqualTo(12));
            Assert.That(mismatch.ClientStateHash, Is.EqualTo(0x1234u));
            Assert.That(mismatch.AuthoritativeStateHash, Is.EqualTo(0x5678u));
            Assert.That(mismatch.ReplayTicks, Is.EqualTo(6));
            Assert.That(replayProgress.Reason, Is.EqualTo(SyncReconciliationReason.None),
                "A cumulative mismatch counter must not emit the same mismatch every tick.");
            Assert.That(replayProgress.RecoveryState, Is.EqualTo(SyncRecoveryState.CatchUp));
        }

        [Test]
        public void PredictionReconciliationReporter_ReportsReplayCompletionOnce()
        {
            var reporter = new MobaPredictionReconciliationReporter();
            var replaying = CreatePredictionSample(1, 1, isReplaying: true, clientFrame: 18);
            var completed = CreatePredictionSample(1, 1, isReplaying: false, clientFrame: 20);

            reporter.Observe(in replaying);
            var recovered = reporter.Observe(in completed);
            var stable = reporter.Observe(in completed);

            Assert.That(recovered.Reason, Is.EqualTo(SyncReconciliationReason.None));
            Assert.That(recovered.RecoveryState, Is.EqualTo(SyncRecoveryState.Recovered));
            Assert.That(recovered.ReplayTicks, Is.EqualTo(8));
            Assert.That(stable.RecoveryState, Is.EqualTo(SyncRecoveryState.Normal));
            Assert.That(stable.DidReconcile, Is.False);
        }

        [Test]
        public void PredictionReconciliationReporter_ReportsSameTickReplayAsRecovered()
        {
            var reporter = new MobaPredictionReconciliationReporter();
            var completed = CreatePredictionSample(1, 1, isReplaying: false, clientFrame: 20);

            var report = reporter.Observe(in completed);

            Assert.That(report.Reason, Is.EqualTo(SyncReconciliationReason.AuthoritativeHashMismatch));
            Assert.That(report.RecoveryState, Is.EqualTo(SyncRecoveryState.Recovered));
            Assert.That(report.ReplayTicks, Is.EqualTo(8));
        }

        [Test]
        public void ReplicationPipeline_UsesFrameworkHealthEventsForNetworkAndRecoverySignals()
        {
            var strategy = new TestClientSyncStrategy();
            var pipeline = new MobaClientReplicationPipeline(strategy);
            var input = new PlayerInputCommand(
                new FrameIndex(10),
                new PlayerId("1"),
                100,
                Array.Empty<byte>());

            pipeline.SubmitInput(in input);
            pipeline.AcknowledgeInput(8);
            pipeline.ObserveRemote(new MobaRemoteSnapshotSample(1ul, 20, Array.Empty<GatewayStateSyncActorSnapshot>()));
            pipeline.ObserveRemote(new MobaRemoteSnapshotSample(1ul, 23, Array.Empty<GatewayStateSyncActorSnapshot>()));
            pipeline.ObserveRemote(new MobaRemoteSnapshotSample(1ul, 22, Array.Empty<GatewayStateSyncActorSnapshot>()));

            strategy.EnqueueReport(new SyncReconciliationReport(
                SyncReconciliationReason.AuthoritativeHashMismatch,
                SyncRecoveryState.CatchUp,
                needsFullSnapshot: false,
                clientFrame: 24,
                authoritativeFrame: 20,
                clientStateHash: 1u,
                authoritativeStateHash: 2u,
                replayTicks: 2));
            strategy.EnqueueReport(new SyncReconciliationReport(
                SyncReconciliationReason.None,
                SyncRecoveryState.Recovered,
                needsFullSnapshot: false,
                clientFrame: 24,
                authoritativeFrame: 20,
                clientStateHash: 1u,
                authoritativeStateHash: 2u,
                replayTicks: 4));
            pipeline.Tick(1f / 30f);
            pipeline.Tick(1f / 30f);

            var diagnostics = pipeline.GetDiagnostics();
            Assert.That(GetHealthCount(diagnostics.Health, SyncHealthEventKind.InputAccepted), Is.EqualTo(1));
            Assert.That(GetHealthCount(diagnostics.Health, SyncHealthEventKind.SnapshotReceived), Is.EqualTo(2));
            Assert.That(GetHealthCount(diagnostics.Health, SyncHealthEventKind.SnapshotGap), Is.EqualTo(1));
            Assert.That(GetHealthCount(diagnostics.Health, SyncHealthEventKind.SnapshotStale), Is.EqualTo(1));
            Assert.That(GetHealthCount(diagnostics.Health, SyncHealthEventKind.RollbackStarted), Is.EqualTo(1));
            Assert.That(GetHealthCount(diagnostics.Health, SyncHealthEventKind.ReplayCompleted), Is.EqualTo(1));
            Assert.That(diagnostics.LastObservedFrame, Is.EqualTo(23));

            pipeline.ResetDiagnostics();
            Assert.That(pipeline.CreateHealthReport().EventCount, Is.Zero);
        }

        [Test]
        public void BattleSessionFeature_ExposesEmptyFrameworkHealthReportBeforeRemoteSessionStarts()
        {
            var feature = new BattleSessionFeature(bootstrapper: null);

            Assert.That(feature.SynchronizationHealthReport, Is.SameAs(SyncHealthReport.Empty));
        }

        private static long GetHealthCount(SyncHealthReport report, SyncHealthEventKind kind)
        {
            return report.Kinds.Single(summary => summary.Kind == kind).Count;
        }

        private static MobaPredictionReconciliationSample CreatePredictionSample(
            long totalMismatchCount,
            long totalRollbackCount,
            bool isReplaying,
            int clientFrame)
        {
            return new MobaPredictionReconciliationSample(
                totalMismatchCount,
                totalRollbackCount,
                isReplaying,
                clientFrame,
                replayToFrame: 20,
                lastRollbackFrame: 12,
                mismatchFrame: 12,
                predictedHash: 0x1234u,
                authoritativeHash: 0x5678u);
        }

        private static ActorEntity CreateActor(ActorContext context, Vec3 position, float hp)
        {
            var entity = context.CreateEntity();
            entity.AddTransform(new Transform3(position, Quat.Identity, Vec3.One));
            var attributeContext = new AttributeContext();
            var group = attributeContext.GetOrCreateGroup(Guid.NewGuid().ToString("N"));
            group.SetBase(MobaAttributeIds.HP, hp);
            entity.AddAttributeGroup(group, attributeContext);
            entity.AddResourceContainer(
                new ResourceContainer
                {
                    Map = new Dictionary<ResourceType, ResourceState>
                    {
                        [ResourceType.Hp] = new ResourceState { Current = Fixed64.FromSingle(hp) },
                    },
                },
                true);
            return entity;
        }

        private sealed class TestWorldResolver : IWorldResolver
        {
            private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

            public void Add<T>(T service)
            {
                _services[typeof(T)] = service;
                _services[service.GetType()] = service;
            }

            public object Resolve(Type serviceType) => _services[serviceType];

            public T Resolve<T>() => (T)Resolve(typeof(T));

            public bool TryResolve(Type serviceType, out object instance) =>
                _services.TryGetValue(serviceType, out instance);

            public bool TryResolve<T>(out T instance)
            {
                if (_services.TryGetValue(typeof(T), out var service))
                {
                    instance = (T)service;
                    return true;
                }

                instance = default;
                return false;
            }
        }

        private sealed class TestClientSyncStrategy :
            IClientSyncStrategy<PlayerInputCommand, MobaRemoteSnapshotSample>
        {
            private readonly Queue<SyncReconciliationReport> _reports =
                new Queue<SyncReconciliationReport>();

            public NetworkSyncModel SyncModel => NetworkSyncModel.PredictRollback;
            public bool IsStarted => true;
            public int CurrentFrame { get; private set; }

            public void EnqueueReport(SyncReconciliationReport report) => _reports.Enqueue(report);

            public SyncTickResult Tick(float deltaSeconds)
            {
                CurrentFrame++;
                return new SyncTickResult(1, CurrentFrame, 0u);
            }

            public void SubmitInput(in PlayerInputCommand input)
            {
            }

            public void ObserveRemote(in MobaRemoteSnapshotSample sample)
            {
            }

            public SyncReconciliationReport GetReconciliationReport()
            {
                return _reports.Count > 0
                    ? _reports.Dequeue()
                    : SyncReconciliationReport.None;
            }
        }

        private sealed class TestWorld : IWorld
        {
            public TestWorld(IWorldResolver services)
            {
                Services = services;
            }

            public WorldId Id => new WorldId("moba-sync-test");
            public string WorldType => "moba";
            public IWorldResolver Services { get; }
            public void Initialize() { }
            public void Tick(float deltaTime) { }
            public void Dispose() { }
        }
    }
}
