using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services.EntityConstruction
{
    public static class MobaActorArchetypeAssembler
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
        }

        private static ActorEntity CreateHero(ActorContext context, in MobaEntityInfo info)
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

        private static ActorEntity CreateMinion(ActorContext context, in MobaEntityInfo info)
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

        private static ActorEntity CreateMonster(ActorContext context, in MobaEntityInfo info)
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

        private static ActorEntity CreateProjectile(ActorContext context, in MobaEntityInfo info)
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

        private static ActorEntity CreateSummon(ActorContext context, in MobaEntityInfo info)
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

        private static ActorEntity CreateProjectileLauncher(ActorContext context, in MobaEntityInfo info)
        {
            var entity = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .Build();

            ActorEntityMetaApplier.Apply(entity, in info);
            return entity;
        }

        private static ActorEntity CreateArea(ActorContext context, in MobaEntityInfo info)
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
    }
}
