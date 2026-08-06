using System;
using System.Collections.Generic;
using AbilityKit.Ability.Battle.EntityManager;
using AbilityKit.Battle.SearchTarget;
using AbilityKit.Core.Mathematics;
using SearchVec2 = AbilityKit.Battle.SearchTarget.Vec2;

namespace AbilityKit.Samples.Logic.Samples.Combat
{
    internal enum SampleFaction
    {
        Neutral = 0,
        Heroes = 1,
        Monsters = 2
    }

    internal enum SampleEntityKind
    {
        Hero = 1,
        Monster = 2,
        Totem = 3
    }

    internal enum SampleSkillSchool
    {
        Fire = 1,
        Holy = 2,
        Control = 3
    }

    internal sealed class SampleCombatEntity
    {
        public SampleCombatEntity(int id, string name, SampleFaction faction, SampleEntityKind kind, float x, float z)
        {
            Id = id;
            Name = name;
            Faction = faction;
            Kind = kind;
            Position = new Vec3(x, 0f, z);
        }

        public int Id { get; }
        public string Name { get; }
        public SampleFaction Faction { get; }
        public SampleEntityKind Kind { get; }
        public Vec3 Position { get; set; }
        public float Hp { get; set; } = 120f;
        public float Armor { get; set; } = 20f;
        public float MagicResist { get; set; } = 15f;
        public float PhysicalAttack { get; set; } = 12f;
        public float MagicPower { get; set; } = 18f;
        public ColliderId Collider { get; set; }

        public string Label => $"{Name}#{Id}";
    }

    internal sealed class SampleSkillData
    {
        public SampleSkillData(string name, SampleSkillSchool school, int cooldownMs, params string[] tags)
        {
            Name = name;
            School = school;
            CooldownMs = cooldownMs;
            Tags = tags ?? Array.Empty<string>();
        }

        public string Name { get; }
        public SampleSkillSchool School { get; }
        public int CooldownMs { get; }
        public string[] Tags { get; }
    }

    internal sealed class SampleCombatWorld
    {
        public const int HeroLayer = 1 << 0;
        public const int MonsterLayer = 1 << 1;

        private readonly Dictionary<int, SampleCombatEntity> _entities = new();
        private readonly Dictionary<int, int> _actorByCollider = new();

        public BattleEntityManager<int> EntityManager { get; } = new();
        public KeyedEntityIndex<SampleFaction, int> ByFaction { get; }
        public KeyedEntityIndex<SampleEntityKind, int> ByKind { get; }
        public MultiKeyEntityIndex<string, int> ByTag { get; }
        public SamplePositionProvider Positions { get; } = new();
        public NaiveCollisionWorld Collision { get; } = new();

        public SampleCombatWorld()
        {
            ByFaction = EntityManager.CreateKeyedIndex<SampleFaction>();
            ByKind = EntityManager.CreateKeyedIndex<SampleEntityKind>();
            ByTag = EntityManager.CreateMultiKeyIndex<string>(StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyCollection<SampleCombatEntity> Entities => _entities.Values;

        public SampleCombatEntity Add(SampleCombatEntity entity, params string[] tags)
        {
            EntityManager.Add(entity.Id);
            ByFaction.SetKey(entity.Id, entity.Faction);
            ByKind.SetKey(entity.Id, entity.Kind);

            foreach (var tag in tags ?? Array.Empty<string>())
            {
                ByTag.AddKey(entity.Id, tag);
            }

            Positions.Set(entity.Id, entity.Position.X, entity.Position.Z);

            var layer = entity.Faction == SampleFaction.Heroes ? HeroLayer : MonsterLayer;
            var collider = Collision.Add(
                new Transform3(entity.Position, Quat.Identity, Vec3.One),
                ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.55f)),
                layer);

            entity.Collider = collider;
            _entities[entity.Id] = entity;
            _actorByCollider[collider.Value] = entity.Id;
            return entity;
        }

        public bool TryGet(int actorId, out SampleCombatEntity entity)
        {
            return _entities.TryGetValue(actorId, out entity);
        }

        public bool TryGetByCollider(ColliderId collider, out SampleCombatEntity entity)
        {
            entity = null;
            return _actorByCollider.TryGetValue(collider.Value, out var actorId) &&
                   _entities.TryGetValue(actorId, out entity);
        }

        public void Move(int actorId, float x, float z)
        {
            if (!_entities.TryGetValue(actorId, out var entity)) return;

            entity.Position = new Vec3(x, 0f, z);
            Positions.Set(actorId, x, z);
            Collision.UpdateTransform(entity.Collider, new Transform3(entity.Position, Quat.Identity, Vec3.One));
        }

        public static SampleCombatWorld CreateLane()
        {
            var world = new SampleCombatWorld();

            world.Add(new SampleCombatEntity(1001, "Knight", SampleFaction.Heroes, SampleEntityKind.Hero, 0f, 0f)
            {
                Hp = 160f,
                Armor = 35f,
                MagicResist = 20f,
                PhysicalAttack = 18f,
                MagicPower = 12f
            }, "ally", "frontline");

            world.Add(new SampleCombatEntity(2001, "Goblin", SampleFaction.Monsters, SampleEntityKind.Monster, 5f, 0f)
            {
                Hp = 90f,
                Armor = 15f,
                MagicResist = 10f
            }, "enemy", "small");

            world.Add(new SampleCombatEntity(2002, "Brute", SampleFaction.Monsters, SampleEntityKind.Monster, 8f, 0f)
            {
                Hp = 150f,
                Armor = 40f,
                MagicResist = 18f
            }, "enemy", "elite");

            world.Add(new SampleCombatEntity(2003, "Shaman", SampleFaction.Monsters, SampleEntityKind.Monster, 6f, 2f)
            {
                Hp = 110f,
                Armor = 10f,
                MagicResist = 35f
            }, "enemy", "caster");

            return world;
        }
    }

    internal sealed class SamplePositionProvider : IPositionProvider, IEntityKeyProvider
    {
        private readonly Dictionary<int, SearchVec2> _positions = new();

        public void Set(int actorId, float x, float z)
        {
            _positions[actorId] = new SearchVec2(x, z);
        }

        public bool TryGetPosition(EntityId entity, out SearchVec2 position)
        {
            if (entity.Value <= int.MaxValue &&
                _positions.TryGetValue((int)entity.Value, out var p))
            {
                position = p;
                return true;
            }

            position = SearchVec2.Zero;
            return false;
        }

        public ulong GetKey(EntityId id)
        {
            return id.Value;
        }
    }

    internal sealed class EntityCollectionCandidateProvider : ICandidateProvider
    {
        private readonly IReadOnlyCollection<int> _ids;

        public EntityCollectionCandidateProvider(IReadOnlyCollection<int> ids)
        {
            _ids = ids ?? Array.Empty<int>();
        }

        public void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            foreach (var id in _ids)
            {
                consumer.Consume(new EntityId(id));
            }
        }
    }

    internal static class CombatSampleFormatting
    {
        public static string FormatIds(IEnumerable<EntityId> ids)
        {
            var parts = new List<string>();
            foreach (var id in ids)
            {
                parts.Add(id.Value.ToString());
            }

            return parts.Count == 0 ? "[]" : "[" + string.Join(", ", parts) + "]";
        }

        public static string FormatVec3(in Vec3 value)
        {
            return $"({value.X:F1}, {value.Y:F1}, {value.Z:F1})";
        }
    }
}
