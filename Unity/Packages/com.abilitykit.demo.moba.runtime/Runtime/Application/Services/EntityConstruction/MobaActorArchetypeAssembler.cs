using System;
using System.Collections.Generic;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services.EntityConstruction
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class MobaActorArchetypeAttribute : Attribute
    {
        public MobaEntityKind Kind { get; }

        public MobaActorArchetypeAttribute(MobaEntityKind kind)
        {
            Kind = kind;
        }
    }

    public static partial class MobaActorArchetypeAssembler
    {
        public static void RegisterDefaults(MobaActorArchetypeRegistry registry)
        {
            if (registry == null) return;
            registry.Register(MobaEntityKind.Hero, CreateHero);
            registry.Register(MobaEntityKind.Minion, CreateMinion);
            registry.Register(MobaEntityKind.Monster, CreateMonster);
            registry.Register(MobaEntityKind.Projectile, CreateProjectile);
            registry.Register(MobaEntityKind.Summon, CreateSummon);
            registry.Register(MobaEntityKind.ProjectileLauncher, CreateProjectileLauncher);
            registry.Register(MobaEntityKind.Area, CreateArea);
            registry.Register(MobaEntityKind.Building, CreateBuilding);
            AddGenerated(registry);
        }

        static partial void AddGenerated(MobaActorArchetypeRegistry registry);

        [MobaActorArchetype(MobaEntityKind.Hero)]
        internal static ActorEntity CreateHero(ActorContext context, in MobaEntityInfo info)
        {
            var entity = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .WithMotion()
                .WithMoveInput()
                .WithCollider(ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.5f)))
                .WithCollisionLayer(layerMask: MobaCollisionLayers.UnitMask)
                .Build();

            ActorEntityMetaApplier.Apply(entity, in info);
            return entity;
        }

        [MobaActorArchetype(MobaEntityKind.Minion)]
        internal static ActorEntity CreateMinion(ActorContext context, in MobaEntityInfo info)
        {
            var entity = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .WithMotion()
                .WithMoveInput()
                .WithCollider(ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.5f)))
                .WithCollisionLayer(layerMask: MobaCollisionLayers.UnitMask)
                .Build();

            ActorEntityMetaApplier.Apply(entity, in info);
            return entity;
        }

        [MobaActorArchetype(MobaEntityKind.Monster)]
        internal static ActorEntity CreateMonster(ActorContext context, in MobaEntityInfo info)
        {
            var entity = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .WithMotion()
                .WithCollider(ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.6f)))
                .WithCollisionLayer(layerMask: MobaCollisionLayers.UnitMask)
                .Build();

            ActorEntityMetaApplier.Apply(entity, in info);
            return entity;
        }

        [MobaActorArchetype(MobaEntityKind.Projectile)]
        internal static ActorEntity CreateProjectile(ActorContext context, in MobaEntityInfo info)
        {
            var entity = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .WithCollider(ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.15f)))
                .WithCollisionLayer(layerMask: MobaCollisionLayers.ProjectileMask)
                .Build();

            ActorEntityMetaApplier.Apply(entity, in info);
            return entity;
        }

        [MobaActorArchetype(MobaEntityKind.Summon)]
        internal static ActorEntity CreateSummon(ActorContext context, in MobaEntityInfo info)
        {
            var entity = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .WithMotion()
                .WithMoveInput()
                .WithCollider(ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.5f)))
                .WithCollisionLayer(layerMask: MobaCollisionLayers.UnitMask)
                .Build();

            ActorEntityMetaApplier.Apply(entity, in info);
            return entity;
        }

        [MobaActorArchetype(MobaEntityKind.ProjectileLauncher)]
        internal static ActorEntity CreateProjectileLauncher(ActorContext context, in MobaEntityInfo info)
        {
            var entity = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .Build();

            ActorEntityMetaApplier.Apply(entity, in info);
            return entity;
        }

        [MobaActorArchetype(MobaEntityKind.Area)]
        internal static ActorEntity CreateArea(ActorContext context, in MobaEntityInfo info)
        {
            var entity = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .WithCollider(ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.5f)))
                .WithCollisionLayer(layerMask: MobaCollisionLayers.ProjectileMask)
                .Build();

            ActorEntityMetaApplier.Apply(entity, in info);
            return entity;
        }

        [MobaActorArchetype(MobaEntityKind.Building)]
        internal static ActorEntity CreateBuilding(ActorContext context, in MobaEntityInfo info)
        {
            var entity = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .WithCollider(ColliderShape.CreateSphere(new Sphere(Vec3.Zero, 0.8f)))
                .WithCollisionLayer(layerMask: MobaCollisionLayers.UnitMask)
                .Build();
            ActorEntityMetaApplier.Apply(entity, in info);
            return entity;
        }
    }
}
