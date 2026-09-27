using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class YingZhengProjectilePresentationPlayModeTests
    {
        private const int SwordCount = 55;
        private const int SwordVfxId = 90006003;
        private const int SwordTemplateId = 30060301;

        [Test]
        public void Ultimate_FiftyFiveRealPrefabSwords_AreBoundAndRecycled()
        {
            Assert.IsTrue(Application.isPlaying);
            var resources = new BattleViewResourceProvider();
            Assert.IsNotNull(Resources.Load<GameObject>("effect/yingzheng_skill3_flying_sword"));
            Assert.IsNotNull(resources.TryGetProjectile(SwordTemplateId));
            var db = resources.GetOrLoadVfxDb();
            Assert.IsTrue(db.TryGet(SwordVfxId, out var config));
            Assert.AreEqual("effect/yingzheng_skill3_flying_sword", config.Resource);

            var world = new EntityWorld();
            var root = world.Create("YingZhengPresentationTestVfx");
            var lookup = new BattleEntityLookup();
            var query = new BattleEntityQuery(world, lookup);
            var manager = new BattleVfxManager(db);
            var handler = new BattleProjectileViewEventHandler(world, query, manager, in root, resources);
            var projectiles = new IEntity[SwordCount];
            var transforms = new BattleTransformComponent[SwordCount];
            try
            {
                for (var wave = 0; wave < 11; wave++)
                {
                    var entries = new ProjectileEventData[5];
                    for (var sword = 0; sword < 5; sword++)
                    {
                        var index = wave * 5 + sword;
                        projectiles[index] = world.Create("SwordProjectile_" + index);
                        transforms[index] = new BattleTransformComponent
                        {
                            Position = new Vector3(0f, 0f, index * 0.1f),
                            Forward = Vector3.right,
                        };
                        projectiles[index].WithRef(transforms[index]);
                        lookup.Bind(new AbilityKit.Game.Battle.Entity.BattleNetId(100000 + index), projectiles[index]);
                        entries[sword] = Event(index, ProjectilePresentationEventKind.Spawn);
                    }

                    handler.HandleSnapshot(entries);
                    handler.HandleSnapshot(entries);
                    handler.Tick();
                    manager.Tick(root, null, query);
                    Assert.AreEqual((wave + 1) * 5, root.ChildCount,
                        "Duplicate spawn or missing VFX at wave " + wave);
                    Assert.LessOrEqual(manager.PoolForStats.DebugStats.Active, SwordCount);
                }

                Assert.AreEqual(SwordCount, root.ChildCount);
                Assert.AreEqual(128, world.TotalCapacity,
                    "The 111 actor and VFX entities should require only one growth from the initial 64 slots.");
                for (var i = 0; i < SwordCount; i++)
                {
                    transforms[i].Position = new Vector3(i + 1f, 0f, 0f);
                }
                manager.Tick(root, null, query);
                for (var i = 0; i < root.ChildCount; i++)
                {
                    var entity = root.GetChild(i);
                    Assert.IsTrue(entity.TryGetRef(out BattleViewGameObjectComponent view));
                    Assert.IsTrue(entity.TryGetRef(out BattleViewFollowComponent follow));
                    Assert.IsNotNull(view.GameObject);
                    Assert.IsNotNull(view.GameObject.transform.Find("SoulCore"),
                        "The real flying-sword prefab must be instantiated, not a placeholder.");
                    Assert.AreEqual(follow.TargetActorId - 100000 + 1f,
                        view.GameObject.transform.position.x, 0.001f,
                        "The real GameObject must follow its projectile actor after a snapshot update.");
                }

                var exits = new ProjectileEventData[SwordCount];
                for (var i = 0; i < SwordCount; i++)
                {
                    exits[i] = Event(i, ProjectilePresentationEventKind.Exit);
                }
                handler.HandleSnapshot(exits);

                handler.Tick();
                manager.Tick(root, null, query);
                Assert.AreEqual(0, root.ChildCount, "All following VFX must leave the view world on exit.");
                Assert.AreEqual(0, manager.PoolForStats.DebugStats.Active);
                Assert.LessOrEqual(manager.PoolForStats.CountInPool, 32);
            }
            finally
            {
                handler.Clear();
                manager.Clear(root);
                if (root.IsValid) world.DestroyRecursive(root.Id);
                for (var i = 0; i < projectiles.Length; i++)
                {
                    if (projectiles[i].IsValid) world.DestroyRecursive(projectiles[i].Id);
                }
            }
        }

        private static ProjectileEventData Event(int index, ProjectilePresentationEventKind kind)
        {
            var id = 100000 + index;
            return new ProjectileEventData(kind, id, 1, SwordTemplateId, 1, 1,
                0f, 0f, index * 0.1f, 0, 0, id, 1f, 0f, 0f);
        }
    }
}
