using System;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using Entitas;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems
{
    [WorldSystem(WorldSystemOrder.MobaBase + WorldSystemOrder.Early, Phase = WorldSystemPhase.PreExecute)]
    public sealed class CollisionWorldSyncSystem : WorldSystemBase
    {
        private readonly ICollisionWorld _world;
        private readonly IGroup<global::ActorEntity> _withShape;
        private readonly IGroup<global::ActorEntity> _withCollisionId;

        public CollisionWorldSyncSystem(global::Entitas.IContexts contexts, IWorldServices services)
            : base(contexts, services)
        {
            if (!services.TryGet<ICollisionService>(out var svc) || svc == null)
            {
                throw new InvalidOperationException("ICollisionService not registered");
            }

            _world = svc.World;
            var ctx = (global::Contexts)contexts;
            _withShape = ctx.actor.GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.Transform,
                global::ActorComponentsLookup.Collider));
            _withCollisionId = ctx.actor.GetGroup(ActorMatcher.CollisionId);
        }

        protected override void OnExecute()
        {
            // Add / Update all active colliders.
            var entities = _withShape.GetEntities();
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null) continue;
                if (!e.hasTransform || !e.hasCollider) continue;

                var t = e.transform.Value;
                var shape = e.collider.LocalShape;
                var layerMask = e.hasCollisionLayer ? e.collisionLayer.Mask : -1;

                if (!e.hasCollisionId)
                {
                    var id = _world.Add(t, shape, layerMask);
                    e.AddCollisionId(id);
                }
                else
                {
                    var id = e.collisionId.Value;
                    _world.Update(id, t, shape);
                    _world.UpdateLayer(id, layerMask);
                }
            }

            // Remove colliders that are no longer valid (lost Transform/Collider).
            var withIds = _withCollisionId.GetEntities();
            for (int i = 0; i < withIds.Length; i++)
            {
                var e = withIds[i];
                if (e == null) continue;
                if (!e.hasCollisionId) continue;

                if (!e.hasTransform || !e.hasCollider)
                {
                    var id = e.collisionId.Value;
                    _world.Remove(id);
                    e.RemoveCollisionId();
                }
            }
        }
    }
}

