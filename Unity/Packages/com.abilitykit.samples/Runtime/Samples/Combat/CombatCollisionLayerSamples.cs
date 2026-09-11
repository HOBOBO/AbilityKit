#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Combat
{
    /// <summary>
    /// Sample layer constants for collision examples
    /// </summary>
    internal static class SampleCollisionLayers
    {
        // Layer IDs (0-63)
        public const int Default = 0;
        public const int Player = 1;
        public const int Monster = 2;
        public const int Projectile = 3;
        public const int Building = 4;
        public const int Terrain = 5;

        // Layer masks for convenience
        public const int PlayerMask = 1 << Player;
        public const int MonsterMask = 1 << Monster;
        public const int ProjectileMask = 1 << Projectile;
        public const int BuildingMask = 1 << Building;
        public const int TerrainMask = 1 << Terrain;
        public const int AllMask = ~0;
    }

    internal sealed class LayerTestWorld
    {
        private readonly List<(string Name, ColliderId Collider, int Layer)> _entities = new List<(string, ColliderId, int)>();
        private readonly NaiveCollisionWorld _collision = new NaiveCollisionWorld();

        public NaiveCollisionWorld Collision => _collision;

        public ColliderId AddCollider(string name, int layerId, float x, float z)
        {
            var transform = new Transform3(new Vec3(x, 0f, z), Quat.Identity, Vec3.One);
            var shape = ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.5f));
            var collider = _collision.Add(transform, shape, layerId);
            _entities.Add((name, collider, layerId));
            return collider;
        }

        public List<(string Name, ColliderId Collider, int Layer)> GetEntities()
        {
            return _entities;
        }
    }

    [Sample(708, "combat", "collision", "layer", "package-api", "web")]
    public sealed class CombatLayerFilterIncludeExclude : SampleBase
    {
        public override string Title => "Combat Layer Filter IncludeExclude";
        public override string Description => "使用双向 LayerFilter（IncludeMask + ExcludeMask）控制碰撞检测范围";
        public override SampleCategory Category => SampleCategory.Combat;

        protected override void OnRun()
        {
            var world = CreateLayerTestWorld();

            Section("Layer 配置");
            KeyValue("Player(1) at", "(0, 0)");
            KeyValue("Ally(2) at", "(2, 0)");
            KeyValue("Enemy(3) at", "(4, 0)");
            KeyValue("Building(4) at", "(6, 0)");
            KeyValue("QueryCenter", "(0, 0)");
            KeyValue("QueryRadius", "8");

            Divider();
            Section("测试 1: Include Player + Monster, Exclude None");
            var filter1 = new LayerFilter(SampleCollisionLayers.PlayerMask | SampleCollisionLayers.MonsterMask);
            var results1 = QuerySphere(world, new Sphere(new Vec3(0, 0, 0), 8f), filter1);
            KeyValue("Filter", filter1.ToString());
            KeyValue("Results", string.Join(", ", results1));
            KeyValue("Expected", "Player, Ally, Enemy, Enemy2");

            Divider();
            Section("测试 2: Include All, Exclude Building");
            var filter2 = LayerFilter.IncludeExclude(SampleCollisionLayers.AllMask, SampleCollisionLayers.BuildingMask);
            var results2 = QuerySphere(world, new Sphere(new Vec3(0, 0, 0), 8f), filter2);
            KeyValue("Filter", filter2.ToString());
            KeyValue("Results", string.Join(", ", results2));
            KeyValue("Expected", "Player, Ally, Enemy, Enemy2");

            Divider();
            Section("测试 3: Only Player (include only)");
            var filter3 = new LayerFilter(SampleCollisionLayers.PlayerMask);
            var results3 = QuerySphere(world, new Sphere(new Vec3(0, 0, 0), 8f), filter3);
            KeyValue("Filter", filter3.ToString());
            KeyValue("Results", string.Join(", ", results3));
            KeyValue("Expected", "Player, Ally");

            Divider();
            Section("测试 4: Empty Include (IncludeMask=0 means no restriction)");
            var filter4 = new LayerFilter(0);
            var results4 = QuerySphere(world, new Sphere(new Vec3(0, 0, 0), 8f), filter4);
            KeyValue("Filter", filter4.ToString());
            KeyValue("Results", string.Join(", ", results4));
            KeyValue("Expected", "Player, Ally, Enemy, Enemy2, Building");

            Divider();
            Section("测试 5: Ignore specific collider");
            var playerCollider = world.GetEntities().First(e => e.Name == "Player").Collider;
            var filter5 = new LayerFilter(SampleCollisionLayers.AllMask, new[] { playerCollider.Value });
            var results5 = QuerySphere(world, new Sphere(new Vec3(0, 0, 0), 8f), filter5);
            KeyValue("Filter", $"IncludeAll, IgnoredColliders=[{playerCollider}]");
            KeyValue("Results", string.Join(", ", results5));
            KeyValue("Expected", "Ally, Enemy, Enemy2, Building");
        }

        private static LayerTestWorld CreateLayerTestWorld()
        {
            var world = new LayerTestWorld();
            world.AddCollider("Player", SampleCollisionLayers.Player, 0f, 0f);
            world.AddCollider("Ally", SampleCollisionLayers.Player, 2f, 0f);
            world.AddCollider("Enemy", SampleCollisionLayers.Monster, 4f, 0f);
            world.AddCollider("Enemy2", SampleCollisionLayers.Monster, 5f, 0f);
            world.AddCollider("Building", SampleCollisionLayers.Building, 6f, 0f);
            return world;
        }

        private static List<string> QuerySphere(LayerTestWorld world, Sphere sphere, LayerFilter filter)
        {
            var results = new List<ColliderId>();
            world.Collision.OverlapSphere(sphere, filter, results);

            var entities = world.GetEntities();
            return results.Select(r =>
            {
                var entity = entities.FirstOrDefault(e => e.Collider == r);
                return entity.Name ?? $"Collider({r.Value})";
            }).ToList();
        }
    }

    [Sample(709, "combat", "collision", "layer-matrix", "package-api", "web")]
    public sealed class CombatLayerMatrixConfiguration : SampleBase
    {
        public override string Title => "Combat Layer Matrix Configuration";
        public override string Description => "通过 ICollisionLayerRelation 配置层之间的关系（Block/Overlap/Ignore）";
        public override SampleCategory Category => SampleCategory.Combat;

        protected override void OnRun()
        {
            var world = new NaiveCollisionWorld();
            var layerRel = world as ICollisionLayerRelation;

            Section("默认层关系");
            KeyValue("Player vs Monster", layerRel.GetRelation(SampleCollisionLayers.Player, SampleCollisionLayers.Monster).ToString());
            KeyValue("Monster vs Monster", layerRel.GetRelation(SampleCollisionLayers.Monster, SampleCollisionLayers.Monster).ToString());
            KeyValue("Player vs Player", layerRel.GetRelation(SampleCollisionLayers.Player, SampleCollisionLayers.Player).ToString());

            Divider();
            Section("配置 MOBA 场景层关系");

            layerRel.SetRelation(SampleCollisionLayers.Player, SampleCollisionLayers.Projectile, CollisionResponse.Block);
            layerRel.SetRelation(SampleCollisionLayers.Monster, SampleCollisionLayers.Projectile, CollisionResponse.Block);
            layerRel.SetRelation(SampleCollisionLayers.Projectile, SampleCollisionLayers.Projectile, CollisionResponse.Ignore);
            layerRel.SetRelation(SampleCollisionLayers.Building, SampleCollisionLayers.Projectile, CollisionResponse.Ignore);
            layerRel.SetRelation(SampleCollisionLayers.Monster, SampleCollisionLayers.Monster, CollisionResponse.Overlap);

            KeyValue("Player vs Projectile", layerRel.GetRelation(SampleCollisionLayers.Player, SampleCollisionLayers.Projectile).ToString());
            KeyValue("Monster vs Projectile", layerRel.GetRelation(SampleCollisionLayers.Monster, SampleCollisionLayers.Projectile).ToString());
            KeyValue("Projectile vs Projectile", layerRel.GetRelation(SampleCollisionLayers.Projectile, SampleCollisionLayers.Projectile).ToString());
            KeyValue("Building vs Projectile", layerRel.GetRelation(SampleCollisionLayers.Building, SampleCollisionLayers.Projectile).ToString());
            KeyValue("Monster vs Monster", layerRel.GetRelation(SampleCollisionLayers.Monster, SampleCollisionLayers.Monster).ToString());

            Divider();
            Section("ShouldCollide 查询");
            KeyValue("Player vs Projectile", world.ShouldCollide(SampleCollisionLayers.Player, SampleCollisionLayers.Projectile).ToString());
            KeyValue("Projectile vs Projectile", world.ShouldCollide(SampleCollisionLayers.Projectile, SampleCollisionLayers.Projectile).ToString());
            KeyValue("Building vs Projectile", world.ShouldCollide(SampleCollisionLayers.Building, SampleCollisionLayers.Projectile).ToString());

            Divider();
            Section("GetLayer 查询");
            var playerCollider = world.Add(new Transform3(Vec3.Zero, Quat.Identity, Vec3.One), ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.5f)), SampleCollisionLayers.Player);
            world.GetLayer(playerCollider, out var layer);
            KeyValue("Player collider layer", layer.ToString());
            KeyValue("Expected", SampleCollisionLayers.Player.ToString());
        }
    }

    [Sample(710, "combat", "collision", "grid", "package-api", "web")]
    public sealed class CombatGridBroadphaseVsNaive : SampleBase
    {
        public override string Title => "Combat GridBroadphase vs Naive";
        public override string Description => "对比 GridCollisionWorld 和 NaiveCollisionWorld 在大量实体下的查询性能";
        public override SampleCategory Category => SampleCategory.Combat;

        protected override void OnRun()
        {
            const int entityCount = 100;
            const int queryCount = 10;
            const float spread = 50f;

            Section("Setup");
            KeyValue("EntityCount", entityCount.ToString());
            KeyValue("SpreadRadius", $"{spread}x{spread}");
            KeyValue("QueryCount", queryCount.ToString());

            var naiveWorld = new NaiveCollisionWorld();
            CreateEntities(naiveWorld, entityCount, spread, 42);

            var gridWorld = new GridCollisionWorld(cellSize: 4f);
            CreateEntities(gridWorld, entityCount, spread, 42);

            Divider();
            Section("NaiveCollisionWorld Queries");
            var naiveFilter = new LayerFilter(0);
            var naiveQueryTime = RunQueriesNaive(naiveWorld, queryCount, naiveFilter);
            KeyValue("NaiveQueryTime", $"{naiveQueryTime:F2}ms");
            KeyValue("NaiveCollision.Performance", $"{naiveQueryTime:F2}ms for {queryCount} queries");

            Divider();
            Section("GridCollisionWorld Queries");
            var gridFilter = new LayerFilter(0);
            var gridQueryTime = RunQueriesGrid(gridWorld, queryCount, gridFilter);
            KeyValue("GridQueryTime", $"{gridQueryTime:F2}ms");
            KeyValue("GridCollision.Performance", $"{gridQueryTime:F2}ms for {queryCount} queries");

            Divider();
            Section("Performance Comparison");
            var speedup = naiveQueryTime / Math.Max(gridQueryTime, 0.001);
            KeyValue("Speedup", $"{speedup:F2}x");
            KeyValue("Winner", speedup > 1 ? "GridCollisionWorld" : "NaiveCollisionWorld");
            KeyValue("Recommendation", entityCount > 50 ? "GridCollisionWorld recommended" : "Either is fine");
        }

        private static void CreateEntities(NaiveCollisionWorld world, int count, float spread, int seed)
        {
            var random = new Random(seed);
            for (int i = 0; i < count; i++)
            {
                var x = (float)(random.NextDouble() * spread - spread / 2);
                var z = (float)(random.NextDouble() * spread - spread / 2);
                var layer = random.Next(0, 4);
                var transform = new Transform3(new Vec3(x, 0f, z), Quat.Identity, Vec3.One);
                var shape = ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.5f));
                world.Add(transform, shape, layer);
            }
        }

        private static void CreateEntities(GridCollisionWorld world, int count, float spread, int seed)
        {
            var random = new Random(seed);
            for (int i = 0; i < count; i++)
            {
                var x = (float)(random.NextDouble() * spread - spread / 2);
                var z = (float)(random.NextDouble() * spread - spread / 2);
                var layer = random.Next(0, 4);
                var transform = new Transform3(new Vec3(x, 0f, z), Quat.Identity, Vec3.One);
                var shape = ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.5f));
                world.Add(transform, shape, layer);
            }
        }

        private static double RunQueriesNaive(NaiveCollisionWorld world, int count, LayerFilter filter)
        {
            var random = new Random(123);
            var results = new List<ColliderId>();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < count; i++)
            {
                var x = (float)(random.NextDouble() * 50 - 25);
                var z = (float)(random.NextDouble() * 50 - 25);
                var sphere = new Sphere(new Vec3(x, 0, z), 5f);
                results.Clear();
                world.OverlapSphere(sphere, filter, results);
            }

            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }

        private static double RunQueriesGrid(GridCollisionWorld world, int count, LayerFilter filter)
        {
            var random = new Random(123);
            var results = new List<ColliderId>();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < count; i++)
            {
                var x = (float)(random.NextDouble() * 50 - 25);
                var z = (float)(random.NextDouble() * 50 - 25);
                var sphere = new Sphere(new Vec3(x, 0, z), 5f);
                results.Clear();
                world.OverlapSphere(sphere, filter, results);
            }

            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }
    }

    [Sample(711, "combat", "collision", "dynamic-tree", "package-api", "web")]
    public sealed class CombatDynamicAabbTree : SampleBase
    {
        public override string Title => "Combat Dynamic AABB Tree";
        public override string Description => "使用 GridCollisionWorld 进行高效的空间查询，支持实体的动态更新";
        public override SampleCategory Category => SampleCategory.Combat;

        protected override void OnRun()
        {
            var world = new GridCollisionWorld();
            var random = new Random(42);

            Section("Initial Setup");
            KeyValue("WorldType", "GridCollisionWorld");

            var colliders = new List<ColliderId>();
            for (int i = 0; i < 10; i++)
            {
                var x = i * 2f;
                var transform = new Transform3(new Vec3(x, 0f, 0f), Quat.Identity, Vec3.One);
                var shape = ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.5f));
                var collider = world.Add(transform, shape, i % 3);
                colliders.Add(collider);
            }

            Divider();
            Section("Initial Query: Sphere at (9, 0, 0), radius=5");
            var filter1 = new LayerFilter(0);
            var results1 = new List<ColliderId>();
            world.OverlapSphere(new Sphere(new Vec3(9, 0, 0), 5f), filter1, results1);
            KeyValue("Results", string.Join(", ", results1.Select(r => r.Value.ToString())));
            KeyValue("CombatDynamicTree.InitialQuery", $"{results1.Count} colliders");

            Divider();
            Section("Dynamic Update: Move Collider(6) from (12,0,0) to (20,0,0)");
            world.UpdateTransform(colliders[5], new Transform3(new Vec3(20, 0, 0), Quat.Identity, Vec3.One));

            Divider();
            Section("Query After Move: Sphere at (9, 0, 0), radius=5");
            var results2 = new List<ColliderId>();
            world.OverlapSphere(new Sphere(new Vec3(9, 0, 0), 5f), filter1, results2);
            KeyValue("Results", string.Join(", ", results2.Select(r => r.Value.ToString())));
            KeyValue("CombatDynamicTree.AfterMove", $"{results2.Count} colliders");

            Divider();
            Section("Query Near New Position: Sphere at (20, 0, 0), radius=5");
            var results3 = new List<ColliderId>();
            world.OverlapSphere(new Sphere(new Vec3(20, 0, 0), 5f), filter1, results3);
            KeyValue("Results", string.Join(", ", results3.Select(r => r.Value.ToString())));
            KeyValue("CombatDynamicTree.NearNewPos", $"{results3.Count} colliders");

            Divider();
            Section("Layer Filtered Query: Sphere at (9, 0, 0), radius=10, layer=2 only");
            var layerFilter = new LayerFilter(1 << 2);
            var results4 = new List<ColliderId>();
            world.OverlapSphere(new Sphere(new Vec3(9, 0, 0), 10f), layerFilter, results4);
            KeyValue("Results", string.Join(", ", results4.Select(r => r.Value.ToString())));
            KeyValue("CombatDynamicTree.LayerFiltered", $"{results4.Count} colliders on layer 2");
        }
    }
}
