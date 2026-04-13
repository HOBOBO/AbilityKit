using System;
using System.Collections.Generic;
using AbilityKit.Core.Math;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;
using Entitas;

namespace AbilityKit.Demo.Moba.Systems
{
    [WorldSystem(MobaSystemOrder.Base + WorldSystemOrder.Early, Phase = WorldSystemPhase.PreExecute)]
    public sealed class CollisionWorldSyncSystem : WorldSystemBase
    {
        private readonly ICollisionWorld _world;
        private readonly IGroup<global::ActorEntity> _withShape;
        private readonly IGroup<global::ActorEntity> _withCollisionId;

        private readonly HashSet<int> _validIds = new HashSet<int>();
        private readonly List<CollisionWorldDebugShape> _worldShapes = new List<CollisionWorldDebugShape>(2048);

        public CollisionWorldSyncSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
            if (!services.TryResolve<ICollisionService>(out var svc) || svc == null)
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
            _validIds.Clear();

            // Add / Update all active colliders.
            var entities = _withShape.GetEntities();
            for (int i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (e == null) continue;
                if (!e.isEnabled) continue;
                if (!e.hasTransform || !e.hasCollider) continue;

                var t = e.transform.Value;
                var shape = e.collider.LocalShape;
                var layerMask = e.hasCollisionLayer ? e.collisionLayer.Mask : -1;

                if (!e.hasCollisionId)
                {
                    var id = _world.Add(t, shape, layerMask);
                    e.AddCollisionId(id);
                    _validIds.Add(id.Value);
                }
                else
                {
                    var id = e.collisionId.Value;
                    _world.Update(id, t, shape);
                    _world.UpdateLayer(id, layerMask);
                    _validIds.Add(id.Value);
                }
            }

            // Remove colliders that are no longer valid (lost Transform/Collider).
            var withIds = _withCollisionId.GetEntities();
            for (int i = 0; i < withIds.Length; i++)
            {
                var e = withIds[i];
                if (e == null) continue;
                if (!e.hasCollisionId) continue;

                if (!e.isEnabled || !e.hasTransform || !e.hasCollider)
                {
                    var id = e.collisionId.Value;
                    _world.Remove(id);
                    e.RemoveCollisionId();
                }
            }

            // Mark-and-sweep cleanup:
            // Some entities may be destroyed/disabled and no longer appear in groups,
            // which would leave stale collider entries in the collision world.
            // We conservatively remove any collider ids that are not associated with
            // currently active (Transform+Collider) entities.
            if (_world is ICollisionWorldDebugView debugView)
            {
                debugView.CopyWorldShapes(_worldShapes);
                for (int i = 0; i < _worldShapes.Count; i++)
                {
                    var id = _worldShapes[i].Id;
                    if (_validIds.Contains(id.Value)) continue;
                    _world.Remove(id);
                }
            }
        }
    }
}

