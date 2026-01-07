using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Effect
{
    public static class ProjectileEffectEventSinkExtensions
    {
        public static void PublishProjectileSpawn(this IEffectEventSink sink, in ProjectileSpawnEvent evt, object source = null, object target = null)
        {
            if (sink == null) return;

            var projectileId = evt.Projectile.Value;
            var ownerId = evt.OwnerId;
            var frame = evt.Frame;
            var position = evt.Position;
            var direction = evt.Direction;

            sink.Publish(ProjectileTriggering.Events.Spawn, payload: null, fillArgs: args =>
            {
                args[EffectTriggering.Args.Source] = source;
                args[EffectTriggering.Args.Target] = target;

                args[ProjectileTriggering.Args.ProjectileId] = projectileId;
                args[ProjectileTriggering.Args.OwnerId] = ownerId;
                args[ProjectileTriggering.Args.Frame] = frame;
                args[ProjectileTriggering.Args.SpawnPosition] = position;
                args[ProjectileTriggering.Args.SpawnDirection] = direction;
            });
        }

        public static void PublishProjectileTick(this IEffectEventSink sink, in ProjectileTickEvent evt, object source = null, object target = null)
        {
            if (sink == null) return;

            var projectileId = evt.Projectile.Value;
            var ownerId = evt.OwnerId;
            var frame = evt.Frame;
            var position = evt.Position;

            sink.Publish(ProjectileTriggering.Events.Tick, payload: null, fillArgs: args =>
            {
                args[EffectTriggering.Args.Source] = source;
                args[EffectTriggering.Args.Target] = target;

                args[ProjectileTriggering.Args.ProjectileId] = projectileId;
                args[ProjectileTriggering.Args.OwnerId] = ownerId;
                args[ProjectileTriggering.Args.Frame] = frame;
                args[ProjectileTriggering.Args.TickPosition] = position;
            });
        }

        public static void PublishProjectileHit(this IEffectEventSink sink, in ProjectileHitEvent evt, object source = null, object target = null)
        {
            if (sink == null) return;

            var projectileId = evt.Projectile.Value;
            var ownerId = evt.OwnerId;
            var frame = evt.Frame;

            var hitCollider = evt.HitCollider;
            var hitDistance = evt.Distance;
            var hitPoint = evt.Point;
            var hitNormal = evt.Normal;

            sink.Publish(ProjectileTriggering.Events.Hit, payload: null, fillArgs: args =>
            {
                args[EffectTriggering.Args.Source] = source;
                args[EffectTriggering.Args.Target] = target;

                args[ProjectileTriggering.Args.ProjectileId] = projectileId;
                args[ProjectileTriggering.Args.OwnerId] = ownerId;
                args[ProjectileTriggering.Args.Frame] = frame;

                args[ProjectileTriggering.Args.HitCollider] = hitCollider;
                args[ProjectileTriggering.Args.HitDistance] = hitDistance;
                args[ProjectileTriggering.Args.HitPoint] = hitPoint;
                args[ProjectileTriggering.Args.HitNormal] = hitNormal;
            });
        }

        public static void PublishProjectileExit(this IEffectEventSink sink, in ProjectileExitEvent evt, object source = null, object target = null)
        {
            if (sink == null) return;

            var projectileId = evt.Projectile.Value;
            var ownerId = evt.OwnerId;
            var frame = evt.Frame;
            var pos = evt.Position;
            var reason = (int)evt.Reason;

            sink.Publish(ProjectileTriggering.Events.Exit, payload: null, fillArgs: args =>
            {
                args[EffectTriggering.Args.Source] = source;
                args[EffectTriggering.Args.Target] = target;

                args[ProjectileTriggering.Args.ProjectileId] = projectileId;
                args[ProjectileTriggering.Args.OwnerId] = ownerId;
                args[ProjectileTriggering.Args.Frame] = frame;

                args[ProjectileTriggering.Args.ExitReason] = reason;
                args[ProjectileTriggering.Args.ExitPosition] = pos;
            });
        }
    }
}
