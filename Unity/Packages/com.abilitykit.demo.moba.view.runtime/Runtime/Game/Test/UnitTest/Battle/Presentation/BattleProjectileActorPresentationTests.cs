using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;
using BattleNetId = AbilityKit.Game.Battle.Entity.BattleNetId;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleProjectileActorPresentationTests
    {
        [Test]
        public void PooledAutoplayParticlesRestartOnEveryLeaseWithoutStartingManualEmitters()
        {
            var pool = new BattleVfxGameObjectPool(_ =>
            {
                var root = new GameObject("pooled-particle-replay");
                root.SetActive(false);
                var autoplay = root.AddComponent<ParticleSystem>();
                var autoplayMain = autoplay.main;
                autoplayMain.playOnAwake = true;
                autoplayMain.loop = true;
                autoplayMain.startLifetime = 1f;
                var emission = autoplay.emission;
                emission.enabled = true;
                emission.rateOverTime = 100f;
                var child = new GameObject("manual-emitter");
                child.transform.SetParent(root.transform);
                var manual = child.AddComponent<ParticleSystem>();
                var main = manual.main;
                main.playOnAwake = false;
                return root;
            });
            try
            {
                Assert.IsTrue(pool.TryRent(71005, out var first));
                var autoplay = first.GetComponent<ParticleSystem>();
                var manual = first.transform.GetChild(0).GetComponent<ParticleSystem>();
                autoplay.Simulate(0.2f, false, true, true);
                Assert.Greater(autoplay.particleCount, 0, "The fixture must emit before exercising reuse.");
                manual.Play(false);
                manual.Emit(3);
                Assert.IsTrue(pool.Return(71005, first));
                Assert.IsTrue(pool.TryRent(71005, out var second));
                Assert.AreSame(first, second, "The test must exercise an actual reused instance.");
                Assert.AreEqual(0f, autoplay.time, 0.0001f);
                // EditMode manually advances particles instead of running Unity's
                // native playback loop; assert renewed emission, not isPlaying.
                autoplay.Simulate(0.2f, false, false, true);
                Assert.Greater(autoplay.particleCount, 0);
                Assert.IsFalse(manual.isPlaying, "Authored manual emitters must wait for their own trigger.");
                Assert.AreEqual(0, manual.particleCount);
            }
            finally { pool.Clear(); }
        }

        [TestCase(71001, 53001, "MobaVfx/Jinx/AA_Minigun_Projectile", false)]
        [TestCase(71002, 53002, "MobaVfx/Jinx/AA_Rocket_Projectile", false)]
        [TestCase(71003, 53003, "MobaVfx/Jinx/AA_Minigun_Projectile", true)]
        [TestCase(71004, 53004, "ChampionVfx/14.Void_Annie/Void_Annie_Q_Projectile", true)]
        public void ConfiguredVisualFollowsProjectileActorAndReleasesOnDespawn(int vfxId, int actorId, string resource, bool interpolate)
        {
            var world = new EntityWorld();
            var root = world.Create("projectile-presentation-test");
            var lookup = new BattleEntityLookup();
            var query = new BattleEntityQuery(world, lookup);
            var factory = new BattleEntityFactory(world, lookup, root);
            var actor = factory.CreateProjectile(new BattleNetId(actorId), new BattleNetId(1), 71010);
            var transform = new BattleTransformComponent { Position = new Vector3(7f, 0f, -3f),
                Forward = Vector3.forward, Scale = Vector3.one };
            actor.WithRef(transform);
            var db = new VfxDatabase(new Dictionary<int, VfxDTO>
            {
                [vfxId] = new VfxDTO { Id = vfxId, Resource = resource, DurationMs = 4200,
                    Loop = true, Speed = 0.75f, PlayCount = 2, DelayMs = 20, IntervalMs = 30 }
            });
            var manager = new BattleVfxManager(db);
            var context = new BattleContext { EntityWorld = world, EntityNode = root, EntityLookup = lookup,
                EntityFactory = factory, EntityQuery = query, ViewVfxManager = manager, ViewVfxNode = root };
            var spawner = new BattleProjectileVfxSpawner(world, manager, root);
            var binder = interpolate ? new BattleViewBinder(manager, root) : null;
            var origin = transform.Position;
            var spec = new BattleProjectileVfxSpawnSpec(vfxId, origin, actor.Id, actorId, Quaternion.identity);
            try
            {
                Assert.IsTrue(spawner.TrySpawn(spec));
                IEntity visual = default;
                world.ForEachAlive(entity =>
                {
                    if (entity.TryGetRef(out BattleVfxComponent effect) && effect.VfxId == vfxId) visual = entity;
                });
                Assert.IsTrue(visual.IsValid);
                Assert.IsTrue(visual.TryGetRef(out BattlePresentationLifetimeComponent lifetime));
                Assert.AreEqual(resource, lifetime.Request.ResourceKey);
                Assert.AreEqual(4200, lifetime.Request.DurationMs);
                Assert.AreEqual(0.75f, lifetime.Request.Playback.Speed);
                Assert.IsTrue(lifetime.Request.Playback.Loop);
                Assert.AreEqual(2, lifetime.Request.Playback.PlayCount);
                Assert.AreEqual(20, lifetime.Request.Playback.DelayMs);
                Assert.AreEqual(30, lifetime.Request.Playback.IntervalMs);
                Assert.AreEqual(actorId, lifetime.Request.Attachment.ActorId);
                Assert.IsTrue(lifetime.Request.FollowAttachment);
                Assert.IsTrue(visual.TryGetRef(out BattleViewGameObjectComponent view));
                for (var sample = 0; sample < 5; sample++)
                {
                    transform.Position = origin + Vector3.forward * (sample * 1.5f);
                    context.LastFrame = 100 + sample;
                    context.LogicTimeSeconds = context.LastFrame / 30d;
                    binder?.TickInterpolation(context, context, 1f / 30f);
                    var expected = transform.Position;
                    if (binder != null)
                    {
                        Assert.IsFalse(binder.TryGetShellGameObject(actor.Id, out _));
                        Assert.IsTrue(binder.TryGetInterpolatedPos(actor.Id, out expected));
                    }
                    manager.Tick(root, binder, query);
                    Assert.Less(Vector3.Distance(expected, view.GameObject.transform.position), 0.001f);
                }
                Assert.Greater(Vector3.Distance(origin, view.GameObject.transform.position), 2f);
                BattleSnapshotEntityApplier.ApplyDespawn(context, new[] { new ActorDespawnData(actorId) });
                Assert.IsFalse(world.IsAlive(visual.Id), "Actor despawn must release the attached projectile visual.");
            }
            finally
            {
                manager.Clear(root);
                if (root.IsValid) root.Destroy();
            }
        }
    }
}
