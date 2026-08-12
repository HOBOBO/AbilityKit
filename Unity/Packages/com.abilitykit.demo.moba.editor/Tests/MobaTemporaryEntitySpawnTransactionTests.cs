using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Projectile;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaTemporaryEntitySpawnTransactionTests
    {
        [Test]
        public void Rollback_ExecutesCompensationsInReverseOrder()
        {
            var calls = new List<int>();
            var transaction = new MobaTemporaryEntitySpawnTransaction();
            transaction.Enlist("first", () => calls.Add(1));
            transaction.Enlist("second", () => calls.Add(2));
            transaction.Enlist("third", () => calls.Add(3));

            transaction.Rollback();

            Assert.That(calls, Is.EqualTo(new[] { 3, 2, 1 }));
        }

        [Test]
        public void Commit_DiscardsCompensations()
        {
            var calls = 0;
            var transaction = new MobaTemporaryEntitySpawnTransaction();
            transaction.Enlist("cleanup", () => calls++);

            transaction.Commit();
            transaction.Dispose();

            Assert.That(calls, Is.Zero);
            Assert.That(transaction.FirstRollbackException, Is.Null);
        }

        [Test]
        public void Rollback_WhenCleanupThrows_ContinuesAndPreservesPrimaryAndFirstCleanupExceptions()
        {
            var calls = new List<string>();
            var diagnostics = new List<string>();
            var primary = new InvalidOperationException("spawn failed");
            var firstCleanup = new ApplicationException("cleanup three failed");
            var transaction = new MobaTemporaryEntitySpawnTransaction(
                (name, exception) => diagnostics.Add(name + ":" + exception.Message));
            transaction.Enlist("one", () => calls.Add("one"));
            transaction.Enlist("two", () =>
            {
                calls.Add("two");
                throw new ArgumentException("cleanup two failed");
            });
            transaction.Enlist("three", () =>
            {
                calls.Add("three");
                throw firstCleanup;
            });

            var reported = transaction.Rollback(primary);

            Assert.That(calls, Is.EqualTo(new[] { "three", "two", "one" }));
            Assert.That(reported, Is.SameAs(primary));
            Assert.That(transaction.PrimaryException, Is.SameAs(primary));
            Assert.That(transaction.FirstRollbackException, Is.SameAs(firstCleanup));
            Assert.That(transaction.RollbackExceptionCount, Is.EqualTo(2));
            Assert.That(diagnostics, Is.EqualTo(new[]
            {
                "three:cleanup three failed",
                "two:cleanup two failed",
            }));
        }

        [Test]
        public void Shoot_WhenProjectileServiceReturnsInvalidId_RollsBackSpawnedActor()
        {
            var contexts = new Contexts();
            var entities = new MobaEntityManager(null);
            var caster = contexts.actor.CreateEntity();
            caster.AddActorId(11);
            caster.AddTransform(Transform3.Identity);
            entities.Register(
                11,
                caster,
                Team.None,
                EntityMainType.Unit,
                UnitSubType.Hero,
                default);

            var actorSpawn = new RecordingActorSpawnService(contexts.actor);
            var projectiles = new RecordingProjectileService(default);
            var service = new MobaProjectileService();
            Inject(service, "_projectiles", projectiles);
            Inject(service, "_actorIds", new ActorIdAllocator());
            Inject(service, "_entities", entities);
            Inject(service, "_actorSpawn", actorSpawn);
            Inject(service, "_frameTime", new FixedFrameTime());

            try
            {
                var aimPosition = new Vec3(1f, 0f, 1f);
                var aimDirection = Vec3.Forward;

                var shot = service.Shoot(
                    11,
                    ProjectileEmitterType.Linear,
                    301,
                    8f,
                    30,
                    0f,
                    in aimPosition,
                    in aimDirection);

                Assert.That(shot, Is.False);
                Assert.That(actorSpawn.SpawnCount, Is.EqualTo(1));
                Assert.That(actorSpawn.RollbackCount, Is.EqualTo(1));
                Assert.That(actorSpawn.LastSpawnResult.ActorId, Is.EqualTo(actorSpawn.LastRollbackResult.ActorId));
                Assert.That(projectiles.DespawnCount, Is.Zero);
            }
            finally
            {
                contexts.actor.DestroyAllEntities();
                entities.Dispose();
            }
        }

        [Test]
        public void Shoot_WhenSourceBindingFails_RollsBackLinkRuntimeAndActor()
        {
            var contexts = new Contexts();
            var entities = new MobaEntityManager(null);
            var caster = contexts.actor.CreateEntity();
            caster.AddActorId(11);
            caster.AddTransform(Transform3.Identity);
            entities.Register(
                11,
                caster,
                Team.None,
                EntityMainType.Unit,
                UnitSubType.Hero,
                default);

            var actorSpawn = new RecordingActorSpawnService(contexts.actor);
            var projectiles = new RecordingProjectileService(new ProjectileId(71));
            var links = new MobaProjectileLinkService();
            var service = new MobaProjectileService();
            Inject(service, "_projectiles", projectiles);
            Inject(service, "_actorIds", new ActorIdAllocator());
            Inject(service, "_entities", entities);
            Inject(service, "_actorSpawn", actorSpawn);
            Inject(service, "_frameTime", new FixedFrameTime());
            Inject(service, "_links", links);

            try
            {
                var aimPosition = new Vec3(1f, 0f, 1f);
                var aimDirection = Vec3.Forward;

                var shot = service.Shoot(
                    11,
                    ProjectileEmitterType.Linear,
                    301,
                    8f,
                    30,
                    0f,
                    in aimPosition,
                    in aimDirection);

                Assert.That(shot, Is.False);
                Assert.That(actorSpawn.RollbackCount, Is.EqualTo(1));
                Assert.That(projectiles.DespawnCount, Is.EqualTo(1));
                Assert.That(links.ActiveCount, Is.Zero);
                Assert.That(links.TryGetActorId(new ProjectileId(71), out _), Is.False);
            }
            finally
            {
                contexts.actor.DestroyAllEntities();
                entities.Dispose();
            }
        }

        private static void Inject(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
            field.SetValue(target, value);
        }

        private sealed class RecordingActorSpawnService : IMobaActorSpawnService, IMobaActorSpawnTransactionService
        {
            private readonly ActorContext _context;

            public RecordingActorSpawnService(ActorContext context)
            {
                _context = context;
            }

            public int SpawnCount { get; private set; }
            public int RollbackCount { get; private set; }
            public MobaActorSpawnResult LastSpawnResult { get; private set; }
            public MobaActorSpawnResult LastRollbackResult { get; private set; }

            public bool TrySpawn(in MobaActorSpawnRequest request, out MobaActorSpawnResult result)
            {
                SpawnCount++;
                var entity = _context.CreateEntity();
                result = new MobaActorSpawnResult(
                    true,
                    request.Spec.Info.ActorId,
                    entity,
                    in request.Spec,
                    null);
                LastSpawnResult = result;
                return true;
            }

            public bool TrySpawnUnpublished(in MobaActorSpawnRequest request, out MobaActorSpawnResult result)
            {
                return TrySpawn(in request, out result);
            }

            public void Publish(in MobaActorSpawnResult result)
            {
            }

            public void Rollback(in MobaActorSpawnResult result)
            {
                RollbackCount++;
                LastRollbackResult = result;
            }

            public void Dispose()
            {
            }
        }

        private sealed class FixedFrameTime : IFrameTime
        {
            public FrameIndex Frame => new FrameIndex(7);
            public float DeltaTime => 1f / 30f;
            public float Time => Frame.Value * DeltaTime;
            public float FrameToTime(FrameIndex frame) => frame.Value * DeltaTime;
            public FrameIndex TimeToFrame(float time) => new FrameIndex((int)Math.Round(time / DeltaTime));
        }

        private sealed class RecordingProjectileService : IProjectileService
        {
            private readonly ProjectileId _spawnResult;

            public RecordingProjectileService(ProjectileId spawnResult)
            {
                _spawnResult = spawnResult;
            }

            public int ActiveCount => 0;
            public int DespawnCount { get; private set; }

            public ProjectileId Spawn(in ProjectileSpawnParams p) => _spawnResult;

            public bool Despawn(ProjectileId id)
            {
                DespawnCount++;
                return true;
            }

            public bool Despawn(ProjectileId id, int frame, ProjectileExitReason reason)
            {
                DespawnCount++;
                return true;
            }

            public bool TryGetRuntimeState(ProjectileId id, out ProjectileRuntimeState state) { state = default; return false; }
            public bool TrySetPosition(ProjectileId id, in Vec3 position) => false;
            public bool ResumeSimulation(ProjectileId id) => false;
            public void Tick(int frame, float fixedDeltaSeconds) { }
            public void DrainSpawnEvents(List<ProjectileSpawnEvent> results) { }
            public void DrainHitEvents(List<ProjectileHitEvent> results) { }
            public void DrainExitEvents(List<ProjectileExitEvent> results) { }
            public void DrainTickEvents(List<ProjectileTickEvent> results) { }
            public void PeekSpawnEvents(List<ProjectileSpawnEvent> results) { }
            public void PeekHitEvents(List<ProjectileHitEvent> results) { }
            public void PeekExitEvents(List<ProjectileExitEvent> results) { }
            public void PeekTickEvents(List<ProjectileTickEvent> results) { }
            public byte[] ExportRollback(FrameIndex frame) => Array.Empty<byte>();
            public void ImportRollback(FrameIndex frame, byte[] payload) { }
            public ProjectileScheduleId ScheduleEmit(IProjectileSpawnPattern pattern, in ProjectileSpawnParams baseSpawn, in ProjectileScheduleParams schedule) => default;
            public ProjectileScheduleId ScheduleEmit(IProjectileSpawnPatternProvider patternProvider, in ProjectileSpawnParams baseSpawn, in ProjectileScheduleParams schedule) => default;
            public bool HasSchedule(ProjectileScheduleId id) => false;
            public bool CancelSchedule(ProjectileScheduleId id) => false;
            public AreaId SpawnArea(in AreaSpawnParams p, int frame) => default;
            public bool DespawnArea(AreaId id, int frame) => false;
            public void DrainAreaSpawnEvents(List<AreaSpawnEvent> results) { }
            public void DrainAreaEnterEvents(List<AreaEnterEvent> results) { }
            public void DrainAreaStayEvents(List<AreaStayEvent> results) { }
            public void DrainAreaExitEvents(List<AreaExitEvent> results) { }
            public void DrainAreaExpireEvents(List<AreaExpireEvent> results) { }
            public void Dispose() { }
        }
    }
}
