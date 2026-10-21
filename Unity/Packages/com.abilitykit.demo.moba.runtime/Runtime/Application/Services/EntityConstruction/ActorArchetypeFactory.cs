using System;
using AbilityKit.Demo.Moba;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services.EntityConstruction
{
    /*
     * 提供按 Archetype（原型/类别）生成 ActorEntity 骨架的能力。
     *
     * 职责边界：
     * - 只负责挂载基础组件（Transform/Motion/Collider 等）和 Meta（Team/Owner 等）。
     * - 不负责读表初始化（属性、技能）。
     * - 不负责批量编排（该部分由 ActorSpawnPipeline 负责）。
     */
    public enum MobaEntityKind
    {
        Unknown = 0,
        Hero = 1,
        Minion = 2,
        Monster = 3,
        Projectile = 4,
        Summon = 5,
        ProjectileLauncher = 6,
        Area = 7,
        Building = 8,
    }

    /* 生成 ActorEntity 骨架所需的最小信息集合。*/
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
        public delegate ActorEntity CreateHandler(ActorContext context, in MobaEntityInfo info);

        private static readonly MobaActorArchetypeRegistry Registry = MobaActorArchetypeRegistry.CreateDefault();

        public static void Register(MobaEntityKind kind, CreateHandler handler)
        {
            Registry.Register(kind, handler);
        }

        public static bool TryCreate(ActorContext context, in MobaEntityInfo info, out ActorEntity entity)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (Registry.TryGet(info.Kind, out var handler))
            {
                entity = handler(context, in info);
                return entity != null;
            }

            entity = null;
            return false;
        }

        public static ActorEntity Create(ActorContext context, in MobaEntityInfo info)
        {
            if (TryCreate(context, in info, out var entity)) return entity;
            throw new InvalidOperationException($"No spawn handler registered for kind={info.Kind}");
        }

        public static MobaEntityKind CreateKindFromType(EntityMainType mainType, UnitSubType unitSubType)
        {
            return MobaEntityKindResolver.Resolve(mainType, unitSubType);
        }
    }
}
