using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Log;
using AbilityKit.Effect;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Impl.Triggering
{
    public sealed class AoeBurstAction : ITriggerAction
    {
        private sealed class AoeBurstTriggerPayload
        {
            public int TriggerId;
            public int SourceActorId;
            public int TargetActorId;
            public Vec3 Center;
            public float Radius;
            public object OriginalPayload;
        }

        private static readonly AbilityKit.Core.Common.Pool.ObjectPool<List<ColliderId>> s_colliderListPool = AbilityKit.Core.Common.Pool.Pools.GetPool(
            key: "AoeBurstAction.ColliderList",
            createFunc: () => new List<ColliderId>(32),
            onRelease: list => list.Clear(),
            defaultCapacity: 16,
            maxSize: 256,
            collectionCheck: false);

        private readonly float _radius;
        private readonly int _collisionLayerMask;
        private readonly int _perTargetTriggerId;
        private readonly int _maxTargets;

        public AoeBurstAction(float radius, int collisionLayerMask, int perTargetTriggerId, int maxTargets)
        {
            _radius = radius;
            _collisionLayerMask = collisionLayerMask;
            _perTargetTriggerId = perTargetTriggerId;
            _maxTargets = maxTargets;
        }

        public static AoeBurstAction FromDef(ActionDef def)
        {
            var args = def?.Args;
            var radius = TriggerActionArgUtil.TryGetFloat(args, "radius", 0f);
            var collisionLayerMask = TriggerActionArgUtil.TryGetInt(args, "collisionLayerMask", -1);
            var perTargetTriggerId = TriggerActionArgUtil.TryGetInt(args, "perTargetTriggerId", 0);
            var maxTargets = TriggerActionArgUtil.TryGetInt(args, "maxTargets", 0);

            return new AoeBurstAction(radius, collisionLayerMask, perTargetTriggerId, maxTargets);
        }

        public void Execute(TriggerContext context)
        {
            if (context == null) return;

            if (_perTargetTriggerId <= 0)
            {
                Log.Warning("[Trigger] aoe_burst requires perTargetTriggerId > 0");
                return;
            }

            var effects = context.Services?.GetService(typeof(MobaEffectExecutionService)) as MobaEffectExecutionService;
            if (effects == null)
            {
                Log.Warning("[Trigger] aoe_burst cannot resolve MobaEffectExecutionService from DI");
                return;
            }

            var registry = context.Services?.GetService(typeof(MobaActorRegistry)) as MobaActorRegistry;
            if (registry == null)
            {
                Log.Warning("[Trigger] aoe_burst cannot resolve MobaActorRegistry from DI");
                return;
            }

            var collisionSvc = context.Services?.GetService(typeof(ICollisionService)) as ICollisionService;
            if (collisionSvc == null || collisionSvc.World == null)
            {
                Log.Warning("[Trigger] aoe_burst cannot resolve ICollisionService from DI");
                return;
            }

            var center = Vec3.Zero;
            var radius = _radius;
            var mask = _collisionLayerMask;
            var maxTargets = _maxTargets;

            var payload = context.Event.Payload;
            if (payload is AreaEventArgs area)
            {
                center = area.Center;
                if (area.Radius > 0f) radius = area.Radius;
                if (area.CollisionLayerMask != 0) mask = area.CollisionLayerMask;
                if (area.MaxTargets > 0) maxTargets = area.MaxTargets;
            }

            if (center.Equals(Vec3.Zero))
            {
                Log.Warning("[Trigger] aoe_burst cannot resolve center (area.center)");
                return;
            }

            if (radius <= 0f)
            {
                Log.Warning("[Trigger] aoe_burst requires radius > 0 (either args[area.radius] or action radius)");
                return;
            }

            List<ColliderId> colliders = null;
            try
            {
                colliders = s_colliderListPool.Get();
                collisionSvc.World.OverlapSphere(new Sphere(center, radius), mask, colliders);

                HashSet<int> uniqueTargets = null;
                if (colliders.Count > 1)
                {
                    uniqueTargets = new HashSet<int>();
                }

                TriggerActionArgUtil.TryResolveActorId(context.Source, out var sourceActorId);
                for (int i = 0; i < colliders.Count; i++)
                {
                    var actorId = ResolveActorIdByCollider(registry, colliders[i]);
                    if (actorId <= 0) continue;

                    if (uniqueTargets != null && !uniqueTargets.Add(actorId)) continue;

                    effects.ExecuteTriggerId(_perTargetTriggerId, new AoeBurstTriggerPayload
                    {
                        TriggerId = _perTargetTriggerId,
                        SourceActorId = sourceActorId,
                        TargetActorId = actorId,
                        Center = center,
                        Radius = radius,
                        OriginalPayload = context.Event.Payload,
                    });

                    if (maxTargets > 0 && uniqueTargets != null && uniqueTargets.Count >= maxTargets) break;
                }
            }
            finally
            {
                if (colliders != null) s_colliderListPool.Release(colliders);
            }
        }

        private static int ResolveActorIdByCollider(MobaActorRegistry registry, ColliderId id)
        {
            if (registry == null) return 0;
            if (id.Value <= 0) return 0;

            try
            {
                foreach (var kv in registry.Entries)
                {
                    var e = kv.Value;
                    if (e == null || !e.hasActorId || !e.hasCollisionId) continue;
                    if (e.collisionId.Value.Equals(id))
                    {
                        return e.actorId.Value;
                    }
                }
            }
            catch
            {
                return 0;
            }

            return 0;
        }
    }
}
