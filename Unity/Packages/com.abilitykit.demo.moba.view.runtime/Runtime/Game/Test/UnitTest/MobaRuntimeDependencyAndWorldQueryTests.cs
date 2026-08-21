using System;
using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Ability.Host.Extensions.Moba.Snapshot;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Testing;
using AbilityKit.Moba.Behavior;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaRuntimeDependencyAndWorldQueryTests
    {
        [Test]
        public void DependencyValidators_EmptyResolver_ReportStableStartupBlockingErrors()
        {
            var validators = new IMobaRuntimeValidator[]
            {
                new MobaRuntimeCoreDependencyValidator(),
                new MobaRuntimeSkillDependencyValidator(),
                new MobaRuntimeContinuousDependencyValidator(),
                new MobaRuntimeCombatDependencyValidator(),
                new MobaRuntimeTemporaryEntityDependencyValidator(),
                new MobaRuntimeOutputDependencyValidator(),
                new MobaRuntimeDiagnosticsDependencyValidator(),
            };
            var context = new MobaRuntimeValidationContext(
                new EmptyWorldResolver(),
                "test",
                MobaRuntimeValidationInvocation.Bootstrap);

            for (var i = 0; i < validators.Length; i++)
            {
                var report = new MobaRuntimeValidationReport();
                validators[i].Validate(in context, report);

                Assert.That(report.ErrorCount, Is.GreaterThan(0), validators[i].Name);
                Assert.That(report.ShouldBlockStartup, Is.True, validators[i].Name);
                Assert.That(report.Entries, Has.Some.Matches<MobaRuntimeValidationEntry>(entry =>
                    entry.Source == validators[i].Name &&
                    entry.Code == MobaRuntimeDependencyValidationRules.MissingDependencyCode &&
                    entry.Category == MobaRuntimeValidationCategory.RuntimeContract &&
                    entry.BlocksStartup), validators[i].Name);
            }
        }

        [Test]
        public void WorldQuery_GetData_ReturnsSupportedScalarValues()
        {
            var query = CreateQuery(allowMutations: false);
            var id = new BehaviorEntityId(7);

            Assert.That(query.GetData<bool>(id, MobaBehaviorContracts.WorldDataKey.Alive), Is.True);
            Assert.That(query.GetData<float>(id, MobaBehaviorContracts.WorldDataKey.HitPoints), Is.EqualTo(125f));
            Assert.That(query.GetData<int>(id, MobaBehaviorContracts.WorldDataKey.Team), Is.EqualTo(2));
            Assert.That(query.GetData<float>(id, MobaBehaviorContracts.WorldDataKey.MoveSpeed), Is.EqualTo(6.5f));
            Assert.That(query.HasData(id, MobaBehaviorContracts.WorldDataKey.Team), Is.True);
            Assert.That(query.HasData(id, MobaBehaviorContracts.WorldDataKey.Tags), Is.False);
        }

        [Test]
        public void WorldQuery_InvalidQueries_FailExplicitly()
        {
            var query = CreateQuery(allowMutations: false);
            var existing = new BehaviorEntityId(7);
            var missing = new BehaviorEntityId(99);

            Assert.Throws<InvalidOperationException>(() => query.GetPosition(missing));
            Assert.Throws<ArgumentException>(() => query.GetData<int>(existing, "unknown"));
            Assert.Throws<InvalidCastException>(() => query.GetData<int>(existing, MobaBehaviorContracts.WorldDataKey.Alive));
            Assert.Throws<NotSupportedException>(() => query.GetData<List<string>>(existing, MobaBehaviorContracts.WorldDataKey.Buffs));
            Assert.Throws<InvalidOperationException>(() => query.SetPosition(existing, Vec3.Zero));
        }

        [Test]
        public void WorldQuery_MutableGenericData_RemainsUnsupported()
        {
            var query = CreateQuery(allowMutations: true);
            var existing = new BehaviorEntityId(7);

            Assert.Throws<NotSupportedException>(() =>
                query.SetData(existing, MobaBehaviorContracts.WorldDataKey.Team, 3));
        }

        [Test]
        public void EnterGameSnapshotTransaction_PrepareHasNoSideEffects_CommitPublishesBothPayloads()
        {
            var enterGameSink = new RecordingEnterGameSnapshotSink();
            var spawnSnapshots = new MobaActorSpawnSnapshotService();
            var transaction = new MobaEnterGameStartupSnapshotTransaction(
                enterGameSink,
                spawnSnapshots);
            var response = CreateEnterGameResponse();
            var spawnEntries = new[]
            {
                new MobaActorSpawnSnapshotEntry(
                    701,
                    (int)SpawnEntityKind.Character,
                    1001,
                    701,
                    1f,
                    0f,
                    2f),
            };

            Assert.That(transaction.TryPrepare(
                in response,
                spawnEntries,
                out var batch,
                out var failureCode,
                out var error), Is.True, error);
            Assert.That(failureCode, Is.EqualTo(MobaGameStartFailureCode.None));
            Assert.That(batch.EnterGamePayload, Is.Not.Null.And.Not.Empty);
            Assert.That(batch.SpawnPayload, Is.Not.Null.And.Not.Empty);
            Assert.That(enterGameSink.PublishCount, Is.Zero);
            Assert.That(spawnSnapshots.TryGetSnapshot(new FrameIndex(1), out _), Is.False);

            transaction.Commit(in batch);

            Assert.That(enterGameSink.PublishCount, Is.EqualTo(1));
            Assert.That(enterGameSink.LastPayload, Is.SameAs(batch.EnterGamePayload));
            Assert.That(spawnSnapshots.TryGetSnapshot(new FrameIndex(1), out var spawnSnapshot), Is.True);
            Assert.That(spawnSnapshot.Payload, Is.SameAs(batch.SpawnPayload));
        }

        [Test]
        public void EnterGameSnapshotTransaction_RollbackClearsPartiallyCommittedPayloads()
        {
            var enterGameSink = new RecordingEnterGameSnapshotSink();
            var spawnSnapshots = new MobaActorSpawnSnapshotService();
            var transaction = new MobaEnterGameStartupSnapshotTransaction(
                enterGameSink,
                spawnSnapshots);
            enterGameSink.PublishEnterGameResPayload(new byte[] { 1 });
            spawnSnapshots.PublishSpawnPayload(new byte[] { 2 });

            transaction.Rollback();

            Assert.That(enterGameSink.PublishCount, Is.EqualTo(2));
            Assert.That(enterGameSink.LastPayload, Is.Null);
            Assert.That(spawnSnapshots.TryGetSnapshot(new FrameIndex(2), out _), Is.False);
        }

        [Test]
        public void EnterGameFlow_SnapshotPrepareFailure_CompensatesAndAllowsRetry()
        {
            var fixture = new EnterGameFlowFixture();
            fixture.Snapshots.FailPrepare = true;

            var failed = fixture.Start();

            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.FailureCode, Is.EqualTo(MobaGameStartFailureCode.PublishEnterGameSnapshotFailed));
            fixture.AssertCompensated();

            fixture.Snapshots.FailPrepare = false;
            Assert.That(fixture.Start().Succeeded, Is.True);
            fixture.AssertStarted();
        }

        [Test]
        public void EnterGameFlow_SnapshotPrepareException_CompensatesAndAllowsRetry()
        {
            var fixture = new EnterGameFlowFixture();
            fixture.Snapshots.ThrowOnPrepare = true;

            var failed = fixture.Start();

            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.FailureCode, Is.EqualTo(MobaGameStartFailureCode.PublishEnterGameSnapshotFailed));
            StringAssert.Contains("prepare fault", failed.Message);
            fixture.AssertCompensated();

            fixture.Snapshots.ThrowOnPrepare = false;
            Assert.That(fixture.Start().Succeeded, Is.True);
            fixture.AssertStarted();
        }

        [Test]
        public void EnterGameFlow_SnapshotCommitException_RollsBackPartialCommitAndAllowsRetry()
        {
            var fixture = new EnterGameFlowFixture();
            fixture.Snapshots.ThrowOnCommit = true;

            var failed = fixture.Start();

            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.FailureCode, Is.EqualTo(MobaGameStartFailureCode.GameStartCommitFailed));
            StringAssert.Contains("commit fault", failed.Message);
            Assert.That(fixture.Snapshots.CommitCount, Is.EqualTo(1));
            fixture.AssertCompensated();

            fixture.Snapshots.ThrowOnCommit = false;
            Assert.That(fixture.Start().Succeeded, Is.True);
            fixture.AssertStarted();
        }

        [Test]
        public void EnterGameFlow_ActorPrepareFailure_DoesNotReachSnapshotsAndAllowsRetry()
        {
            var fixture = new EnterGameFlowFixture();
            fixture.ActorSpawns.FailPrepare = true;

            var failed = fixture.Start();

            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.FailureCode, Is.EqualTo(MobaGameStartFailureCode.ActorBuildFailed));
            StringAssert.Contains("actor prepare rejected", failed.Message);
            Assert.That(fixture.Snapshots.PrepareCount, Is.Zero);
            fixture.AssertCompensated();

            fixture.ActorSpawns.FailPrepare = false;
            Assert.That(fixture.Start().Succeeded, Is.True);
            fixture.AssertStarted();
        }

        [Test]
        public void EnterGameFlow_ActorPrepareException_DoesNotReachSnapshotsAndAllowsRetry()
        {
            var fixture = new EnterGameFlowFixture();
            fixture.ActorSpawns.ThrowOnPrepare = true;

            var failed = fixture.Start();

            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.FailureCode, Is.EqualTo(MobaGameStartFailureCode.ActorBuildFailed));
            StringAssert.Contains("actor prepare fault", failed.Message);
            Assert.That(fixture.Snapshots.PrepareCount, Is.Zero);
            fixture.AssertCompensated();

            fixture.ActorSpawns.ThrowOnPrepare = false;
            Assert.That(fixture.Start().Succeeded, Is.True);
            fixture.AssertStarted();
        }

        [Test]
        public void EnterGameFlow_InvalidPreparedActorBatch_IsRejectedAndAllowsRetry()
        {
            var fixture = new EnterGameFlowFixture();
            fixture.ActorSpawns.ReturnInvalidBatch = true;

            var failed = fixture.Start();

            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.FailureCode, Is.EqualTo(MobaGameStartFailureCode.ActorBuildFailed));
            Assert.That(fixture.Snapshots.PrepareCount, Is.Zero);
            fixture.AssertCompensated();

            fixture.ActorSpawns.ReturnInvalidBatch = false;
            Assert.That(fixture.Start().Succeeded, Is.True);
            fixture.AssertStarted();
        }

        [Test]
        public void EnterGameFlow_ActorPublishException_CompensatesAndAllowsRetry()
        {
            var fixture = new EnterGameFlowFixture();
            fixture.ActorSpawns.ThrowOnPublish = true;

            var failed = fixture.Start();

            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.FailureCode, Is.EqualTo(MobaGameStartFailureCode.GameStartCommitFailed));
            StringAssert.Contains("actor publish fault", failed.Message);
            Assert.That(fixture.Snapshots.CommitCount, Is.Zero);
            Assert.That(fixture.Snapshots.RollbackCount, Is.EqualTo(1));
            fixture.AssertCompensated();

            fixture.ActorSpawns.ThrowOnPublish = false;
            Assert.That(fixture.Start().Succeeded, Is.True);
            fixture.AssertStarted();
        }

        [Test]
        public void EnterGameFlow_RollbackException_DoesNotMaskPrimaryFailureAndAllowsRetry()
        {
            var fixture = new EnterGameFlowFixture();
            fixture.ActorSpawns.ThrowOnPublish = true;
            fixture.ActorSpawns.ThrowOnRollback = true;

            var failed = fixture.Start();

            Assert.That(failed.Succeeded, Is.False);
            Assert.That(failed.FailureCode, Is.EqualTo(MobaGameStartFailureCode.GameStartCommitFailed));
            StringAssert.Contains("actor publish fault", failed.Message);
            StringAssert.DoesNotContain("actor rollback fault", failed.Message);
            Assert.That(fixture.ActorSpawns.RollbackCount, Is.EqualTo(1));
            Assert.That(fixture.Snapshots.RollbackCount, Is.EqualTo(1));

            fixture.ActorSpawns.ThrowOnPublish = false;
            fixture.ActorSpawns.ThrowOnRollback = false;
            Assert.That(fixture.Start().Succeeded, Is.True);
            fixture.AssertStarted();
        }

        [Test]
        public void EnterGameFlow_SeparateWorldFixtures_IsolateFailureAndLifecycleState()
        {
            var worldA = new EnterGameFlowFixture("world-a", "player-a");
            var worldB = new EnterGameFlowFixture("world-b", "player-b");
            worldA.Snapshots.FailPrepare = true;

            var failedA = worldA.Start();
            var startedB = worldB.Start();

            Assert.That(failedA.FailureCode, Is.EqualTo(MobaGameStartFailureCode.PublishEnterGameSnapshotFailed));
            worldA.AssertCompensated();
            Assert.That(startedB.Succeeded, Is.True);
            worldB.AssertStarted();

            worldA.Snapshots.FailPrepare = false;
            Assert.That(worldA.Start().Succeeded, Is.True);
            worldA.AssertStarted();
            worldB.AssertStarted();
        }

        private static EnterMobaGameRes CreateEnterGameResponse()
        {
            return new EnterMobaGameRes(
                new WorldId("snapshot-transaction-test"),
                new PlayerId("player-1"),
                localActorId: 701,
                randomSeed: 17,
                tickRate: 30,
                inputDelayFrames: 1);
        }

        private static MobaWorldQuery CreateQuery(bool allowMutations)
        {
            return new MobaWorldQuery(
                new TestEntityManager(7),
                new TestBuffManager(),
                new TestAttributeSystem(),
                allowMutations);
        }

        private sealed class EmptyWorldResolver : IWorldResolver
        {
            public object Resolve(Type serviceType) =>
                throw new InvalidOperationException("Service not registered: " + serviceType);

            public T Resolve<T>() => (T)Resolve(typeof(T));

            public bool TryResolve(Type serviceType, out object instance)
            {
                instance = null;
                return false;
            }

            public bool TryResolve<T>(out T instance)
            {
                instance = default;
                return false;
            }
        }

        private sealed class TestEntityManager : MobaWorldQuery.IEntityManager
        {
            private readonly long _entityId;

            public TestEntityManager(long entityId)
            {
                _entityId = entityId;
            }

            public bool Exists(long entityId) => entityId == _entityId;
            public Vec3 GetPosition(long entityId) => new Vec3(1f, 2f, 3f);
            public void SetPosition(long entityId, Vec3 position) { }
            public Vec3 GetForward(long entityId) => Vec3.Forward;
            public void SetForward(long entityId, Vec3 forward) { }
        }

        private sealed class TestBuffManager : MobaWorldQuery.IBuffManager
        {
            public bool HasBuff(long entityId, string buffId) => buffId == "1001";
            public bool HasTag(long entityId, string tag) => tag == "stunned";
        }

        private sealed class EnterGameFlowFixture
        {
            private const int GameplayId = 9001;
            private readonly PlayerId _playerId;
            private readonly MobaPlayerActorMapService _playerActorMap = new MobaPlayerActorMapService();
            private readonly MobaGameplayService _gameplay;
            private readonly MobaEnterGameFlowService _flow;
            private readonly MobaGameStartSpec _spec;

            public EnterGameFlowFixture(
                string worldId = "enter-game-flow-test",
                string playerId = "flow-player")
            {
                _playerId = new PlayerId(playerId);
                var config = new MobaTestConfigBuilder()
                    .AddDtos(new GameplayDTO
                    {
                        Id = GameplayId,
                        Name = "Flow Transaction Test",
                        TriggerIds = Array.Empty<int>(),
                    })
                    .BuildDatabase();
                var resolver = new ConfigWorldResolver(config);
                _gameplay = WorldTestInjector.For(new MobaGameplayService())
                    .With<IWorldResolver>(resolver)
                    .Build();

                var generator = new ActorEntityInitPipeline(resolver);
                var request = CreateStartRequest(_playerId);
                _spec = new MobaGameStartSpec(in request);
                _flow = WorldTestInjector.For(new MobaEnterGameFlowService())
                    .With<IMobaEnterGameStartupSnapshotTransaction>(Snapshots)
                    .With<IWorldContext>(new TestWorldContext(worldId, resolver))
                    .With<global::Entitas.IContexts>(new global::Contexts())
                    .With(new ActorIdAllocator())
                    .With<IMobaActorSpawnCoordinator>(ActorSpawns)
                    .With<IMobaPlayerActorBindingTransaction>(_playerActorMap)
                    .With(generator)
                    .With<IMobaGameplayStartTransaction>(_gameplay)
                    .Build();
            }

            public FaultInjectingSnapshots Snapshots { get; } = new FaultInjectingSnapshots();
            public RecordingActorSpawnCoordinator ActorSpawns { get; } = new RecordingActorSpawnCoordinator();

            public MobaGameStartResult Start() => _flow.TryStartGame(in _spec);

            public void AssertCompensated()
            {
                Assert.That(_gameplay.IsRunning, Is.False);
                Assert.That(_gameplay.Phase, Is.EqualTo(MobaGameplayPhase.NotStarted));
                Assert.That(_playerActorMap.TryGetActorId(_playerId, out _), Is.False);
                Assert.That(ActorSpawns.RollbackCount, Is.GreaterThan(0));
                Assert.That(Snapshots.HasPublishedPayload, Is.False);
            }

            public void AssertStarted()
            {
                Assert.That(_gameplay.IsRunning, Is.True);
                Assert.That(_playerActorMap.TryGetActorId(_playerId, out var actorId), Is.True);
                Assert.That(actorId, Is.EqualTo(ActorSpawns.LastPreparedActorId));
                Assert.That(Snapshots.HasPublishedPayload, Is.True);
            }

            private static EnterMobaGameReq CreateStartRequest(PlayerId playerId)
            {
                var players = new[]
                {
                    new MobaPlayerLoadout(
                        playerId,
                        teamId: 1,
                        heroId: 1001,
                        attributeTemplateId: 1001,
                        level: 1,
                        basicAttackSkillId: 10010011,
                        skillIds: new[] { 10010101 },
                        spawnIndex: 1,
                        hasSpawnPosition: 1,
                        spawnX: 3f,
                        spawnY: 0f,
                        spawnZ: 4f),
                };
                return new EnterMobaGameReq(
                    playerId,
                    matchId: "flow-transaction-test",
                    mapId: 1,
                    randomSeed: 17,
                    tickRate: 30,
                    inputDelayFrames: 1,
                    players: players,
                    gameplayId: GameplayId);
            }
        }

        private sealed class FaultInjectingSnapshots : IMobaEnterGameStartupSnapshotTransaction
        {
            public bool FailPrepare { get; set; }
            public bool ThrowOnPrepare { get; set; }
            public bool ThrowOnCommit { get; set; }
            public bool HasPublishedPayload { get; private set; }
            public int PrepareCount { get; private set; }
            public int CommitCount { get; private set; }
            public int RollbackCount { get; private set; }

            public bool TryPrepare(
                in EnterMobaGameRes response,
                IReadOnlyList<MobaActorSpawnSnapshotEntry> spawnEntries,
                out MobaEnterGameStartupSnapshotBatch batch,
                out MobaGameStartFailureCode failureCode,
                out string error)
            {
                PrepareCount++;
                if (ThrowOnPrepare) throw new InvalidOperationException("prepare fault");
                if (FailPrepare)
                {
                    batch = default;
                    failureCode = MobaGameStartFailureCode.PublishEnterGameSnapshotFailed;
                    error = "prepare rejected";
                    return false;
                }

                batch = new MobaEnterGameStartupSnapshotBatch(new byte[] { 1 }, new byte[] { 2 });
                failureCode = MobaGameStartFailureCode.None;
                error = null;
                return true;
            }

            public void Commit(in MobaEnterGameStartupSnapshotBatch batch)
            {
                CommitCount++;
                HasPublishedPayload = true;
                if (ThrowOnCommit) throw new InvalidOperationException("commit fault");
            }

            public void Rollback()
            {
                RollbackCount++;
                HasPublishedPayload = false;
            }

            public void Dispose()
            {
            }
        }

        private sealed class RecordingActorSpawnCoordinator : IMobaActorSpawnCoordinator
        {
            public bool FailPrepare { get; set; }
            public bool ThrowOnPrepare { get; set; }
            public bool ReturnInvalidBatch { get; set; }
            public bool ThrowOnPublish { get; set; }
            public bool ThrowOnRollback { get; set; }
            public int LastPreparedActorId { get; private set; }
            public int RollbackCount { get; private set; }

            public bool TryPrepareBatch(
                IReadOnlyList<MobaActorSpawnRequest> requests,
                out MobaActorSpawnBatchResult result)
            {
                if (ThrowOnPrepare) throw new InvalidOperationException("actor prepare fault");
                if (FailPrepare)
                {
                    result = MobaActorSpawnBatchResult.Failed(0, "actor prepare rejected");
                    return false;
                }

                if (ReturnInvalidBatch)
                {
                    result = new MobaActorSpawnBatchResult(
                        true,
                        Array.Empty<MobaActorSpawnResult>(),
                        -1,
                        null);
                    return true;
                }

                var actors = new MobaActorSpawnResult[requests.Count];
                for (var i = 0; i < requests.Count; i++)
                {
                    var spec = requests[i].Spec;
                    LastPreparedActorId = spec.Info.ActorId;
                    actors[i] = new MobaActorSpawnResult(
                        true,
                        spec.Info.ActorId,
                        null,
                        in spec,
                        null,
                        registeredActor: true,
                        registeredEntityManager: true);
                }

                result = new MobaActorSpawnBatchResult(true, actors, -1, null);
                return true;
            }

            public void PublishBatch(IReadOnlyList<MobaActorSpawnResult> actors)
            {
                if (ThrowOnPublish) throw new InvalidOperationException("actor publish fault");
            }

            public void RollbackBatch(IReadOnlyList<MobaActorSpawnResult> actors)
            {
                RollbackCount++;
                if (ThrowOnRollback) throw new InvalidOperationException("actor rollback fault");
            }

            public bool TrySpawnBatch(
                IReadOnlyList<MobaActorSpawnRequest> requests,
                out MobaActorSpawnBatchResult result)
            {
                return TryPrepareBatch(requests, out result);
            }

            public void Dispose()
            {
            }
        }

        private sealed class TestWorldContext : IWorldContext
        {
            public TestWorldContext(string worldId, IWorldResolver services)
            {
                Id = new WorldId(worldId);
                Services = services;
            }

            public WorldId Id { get; }
            public string WorldType => "moba-test";
            public IWorldResolver Services { get; }
        }

        private sealed class ConfigWorldResolver : IWorldResolver
        {
            private readonly MobaConfigDatabase _config;

            public ConfigWorldResolver(MobaConfigDatabase config)
            {
                _config = config;
            }

            public object Resolve(Type serviceType)
            {
                if (TryResolve(serviceType, out var instance)) return instance;
                throw new InvalidOperationException("Service not registered: " + serviceType);
            }

            public T Resolve<T>() => (T)Resolve(typeof(T));

            public bool TryResolve(Type serviceType, out object instance)
            {
                if (serviceType == typeof(MobaConfigDatabase))
                {
                    instance = _config;
                    return true;
                }

                instance = null;
                return false;
            }

            public bool TryResolve<T>(out T instance)
            {
                if (TryResolve(typeof(T), out var value) && value is T typed)
                {
                    instance = typed;
                    return true;
                }

                instance = default;
                return false;
            }
        }

        private sealed class RecordingEnterGameSnapshotSink : IMobaEnterGameSnapshotSink
        {
            public int PublishCount { get; private set; }
            public byte[] LastPayload { get; private set; }

            public void PublishEnterGameResPayload(byte[] payload)
            {
                PublishCount++;
                LastPayload = payload;
            }
        }

        private sealed class TestAttributeSystem : MobaWorldQuery.IAttributeSystem
        {
            public float GetAttribute(long entityId, string attributeId)
            {
                if (attributeId == MobaBehaviorContracts.WorldDataKey.HitPoints) return 125f;
                if (attributeId == MobaBehaviorContracts.WorldDataKey.MoveSpeed) return 6.5f;
                return 0f;
            }

            public bool IsAlive(long entityId) => true;
            public int GetTeam(long entityId) => 2;
        }
    }
}
