using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Math;

namespace AbilityKit.Ability.Share.Impl.Moba.Util.Generator
{
    /*
     * 这里提供“按 Archetype(原型/类别) 生成 ActorEntity 骨架”的能力。
     *
     * 职责边界：
     * - 只负责挂载基础组件（Transform/Motion/Collider 等）与 Meta（Team/Owner 等）。
     * - 不负责读表初始化（属性/技能）。
     * - 不负责批量编排（那部分由 ActorSpawnPipeline 负责）。
     */
    public enum MobaEntityKind
    {
        Unknown = 0,
        Hero = 1,
        Minion = 2,
        Monster = 3,
        Projectile = 4,
    }

    /* 生成 ActorEntity 骨架所需的最小信息集合。 */
    public readonly struct MobaEntityInfo
    {
        public readonly int ActorId;
        public readonly MobaEntityKind Kind;
        public readonly Transform3 Transform;

        public readonly Team Team;
        public readonly EntityMainType MainType;
        public readonly UnitSubType UnitSubType;
        public readonly PlayerId OwnerPlayer;

        public readonly int TemplateId;

        public MobaEntityInfo(
            int actorId,
            MobaEntityKind kind,
            in Transform3 transform,
            Team team,
            EntityMainType mainType,
            UnitSubType unitSubType,
            PlayerId ownerPlayer,
            int templateId = 0)
        {
            ActorId = actorId;
            Kind = kind;
            Transform = transform;

            Team = team;
            MainType = mainType;
            UnitSubType = unitSubType;
            OwnerPlayer = ownerPlayer;

            TemplateId = templateId;
        }
    }

    public static class ActorArchetypeFactory
    {
        private const int CollisionLayer_Unit = 1 << 0;
        private const int CollisionLayer_Projectile = 1 << 1;
        /* World 层当前未使用，保留位用于后续扩展（地形/障碍物等） */
        private const int CollisionLayer_World = 1 << 2;

        public delegate ActorEntity CreateHandler(ActorContext context, in MobaEntityInfo info);

        private static readonly Dictionary<MobaEntityKind, CreateHandler> _handlers = new Dictionary<MobaEntityKind, CreateHandler>
        {
            { MobaEntityKind.Hero, CreateHero },
            { MobaEntityKind.Minion, CreateMinion },
            { MobaEntityKind.Monster, CreateMonster },
            { MobaEntityKind.Projectile, CreateProjectile },
        };

        public static void Register(MobaEntityKind kind, CreateHandler handler)
        {
            if (kind == MobaEntityKind.Unknown) throw new ArgumentException("kind cannot be Unknown", nameof(kind));
            _handlers[kind] = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public static bool TryCreate(ActorContext context, in MobaEntityInfo info, out ActorEntity entity)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (_handlers.TryGetValue(info.Kind, out var handler) && handler != null)
            {
                entity = handler(context, in info);
                return entity != null;
            }

            entity = null;
            return false;
        }

        public static ActorEntity Create(ActorContext context, in MobaEntityInfo info)
        {
            if (TryCreate(context, in info, out var e)) return e;
            throw new InvalidOperationException($"No spawn handler registered for kind={info.Kind}");
        }

        public static MobaEntityKind CreateKindFromType(EntityMainType mainType, UnitSubType unitSubType)
        {
            if (mainType != EntityMainType.Unit) return MobaEntityKind.Hero;

            switch (unitSubType)
            {
                case UnitSubType.Minion:
                    return MobaEntityKind.Minion;
                case UnitSubType.Neutral:
                case UnitSubType.Boss:
                    return MobaEntityKind.Monster;
                default:
                    return MobaEntityKind.Hero;
            }
        }

        private static ActorEntity CreateHero(ActorContext context, in MobaEntityInfo info)
        {
            /* Hero 的骨架组件组合（Transform + Motion + Input + Collider + Meta） */
            var b = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .WithMotion()
                .WithMoveInput()
                .WithCollider(ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.5f)))
                .WithCollisionLayer(layerMask: CollisionLayer_Unit);

            var e = b.Build();
            ApplyMeta(e, in info);
            return e;
        }

        private static ActorEntity CreateProjectile(ActorContext context, in MobaEntityInfo info)
        {
            /* Projectile 的骨架组件组合（Transform + Collider + Meta） */
            var b = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .WithCollider(ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.15f)))
                .WithCollisionLayer(layerMask: CollisionLayer_Projectile);

            var e = b.Build();
            ApplyMeta(e, in info);
            return e;
        }

        private static ActorEntity CreateMinion(ActorContext context, in MobaEntityInfo info)
        {
            /* Minion 的骨架组件组合（Transform + Motion + Collider + Meta） */
            var b = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .WithMotion()
                .WithCollider(ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.5f)))
                .WithCollisionLayer(layerMask: CollisionLayer_Unit);

            var e = b.Build();
            ApplyMeta(e, in info);
            return e;
        }

        private static ActorEntity CreateMonster(ActorContext context, in MobaEntityInfo info)
        {
            /* Monster 的骨架组件组合（Transform + Motion + Collider + Meta） */
            var b = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .WithMotion()
                .WithCollider(ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.6f)))
                .WithCollisionLayer(layerMask: CollisionLayer_Unit);

            var e = b.Build();
            ApplyMeta(e, in info);
            return e;
        }

        private static void ApplyMeta(ActorEntity e, in MobaEntityInfo info)
        {
            if (e == null) return;

            if (e.hasTeam) e.ReplaceTeam(info.Team);
            else e.AddTeam(info.Team);

            if (e.hasEntityMainType) e.ReplaceEntityMainType(info.MainType);
            else e.AddEntityMainType(info.MainType);

            if (e.hasUnitSubType) e.ReplaceUnitSubType(info.UnitSubType);
            else e.AddUnitSubType(info.UnitSubType);

            if (e.hasOwnerPlayerId) e.ReplaceOwnerPlayerId(info.OwnerPlayer);
            else e.AddOwnerPlayerId(info.OwnerPlayer);

        }
    }
}
