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
            var templateId = evt.TemplateId;
            var launcherActorId = evt.LauncherActorId;
            var rootActorId = evt.RootActorId;
            var frame = evt.Frame;
            var position = evt.Position;
            var direction = evt.Direction;

            sink.Publish(ProjectileTriggering.Events.Spawn, payload: evt, fillArgs: args =>
            {
                args[EffectTriggering.Args.Source] = source;
                args[EffectTriggering.Args.Target] = target;
                args[EffectTriggering.Args.OriginSource] = source;
                args[EffectTriggering.Args.OriginTarget] = target;

                args[ProjectileTriggering.Args.ProjectileId] = projectileId;
                args[ProjectileTriggering.Args.OwnerId] = ownerId;
                args[ProjectileTriggering.Args.TemplateId] = templateId;
                args[ProjectileTriggering.Args.LauncherActorId] = launcherActorId;
                args[ProjectileTriggering.Args.RootActorId] = rootActorId;
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
            var templateId = evt.TemplateId;
            var launcherActorId = evt.LauncherActorId;
            var rootActorId = evt.RootActorId;
            var frame = evt.Frame;
            var position = evt.Position;

            sink.Publish(ProjectileTriggering.Events.Tick, payload: evt, fillArgs: args =>
            {
                args[EffectTriggering.Args.Source] = source;
                args[EffectTriggering.Args.Target] = target;
                args[EffectTriggering.Args.OriginSource] = source;
                args[EffectTriggering.Args.OriginTarget] = target;

                args[ProjectileTriggering.Args.ProjectileId] = projectileId;
                args[ProjectileTriggering.Args.OwnerId] = ownerId;
                args[ProjectileTriggering.Args.TemplateId] = templateId;
                args[ProjectileTriggering.Args.LauncherActorId] = launcherActorId;
                args[ProjectileTriggering.Args.RootActorId] = rootActorId;
                args[ProjectileTriggering.Args.Frame] = frame;
                args[ProjectileTriggering.Args.TickPosition] = position;
            });
        }

        public static void PublishProjectileHit(this IEffectEventSink sink, in ProjectileHitEvent evt, object source = null, object target = null)
        {
            if (sink == null) return;

            var projectileId = evt.Projectile.Value;
            var ownerId = evt.OwnerId;
            var templateId = evt.TemplateId;
            var launcherActorId = evt.LauncherActorId;
            var rootActorId = evt.RootActorId;
            var frame = evt.Frame;

            var hitCollider = evt.HitCollider;
            var hitDistance = evt.Distance;
            var hitPoint = evt.Point;
            var hitNormal = evt.Normal;

            var hitCount = evt.HitCount;
            var hitDecayRate = hitCount <= 1 ? 1f : (float)System.Math.Pow(0.8d, hitCount - 1);

            sink.Publish(ProjectileTriggering.Events.Hit, payload: evt, fillArgs: args =>
            {
                args[EffectTriggering.Args.Source] = source;
                args[EffectTriggering.Args.Target] = target;
                args[EffectTriggering.Args.OriginSource] = source;
                args[EffectTriggering.Args.OriginTarget] = target;

                args[ProjectileTriggering.Args.ProjectileId] = projectileId;
                args[ProjectileTriggering.Args.OwnerId] = ownerId;
                args[ProjectileTriggering.Args.TemplateId] = templateId;
                args[ProjectileTriggering.Args.LauncherActorId] = launcherActorId;
                args[ProjectileTriggering.Args.RootActorId] = rootActorId;
                args[ProjectileTriggering.Args.Frame] = frame;

                args[ProjectileTriggering.Args.HitCollider] = hitCollider;
                args[ProjectileTriggering.Args.HitDistance] = hitDistance;
                args[ProjectileTriggering.Args.HitPoint] = hitPoint;
                args[ProjectileTriggering.Args.HitNormal] = hitNormal;

                args[ProjectileTriggering.Args.HitCount] = hitCount;
                args[ProjectileTriggering.Args.HitDecayRate] = hitDecayRate;
            });
        }

        public static void PublishProjectileExit(this IEffectEventSink sink, in ProjectileExitEvent evt, object source = null, object target = null)
        {
            if (sink == null) return;

            var projectileId = evt.Projectile.Value;
            var ownerId = evt.OwnerId;
            var templateId = evt.TemplateId;
            var launcherActorId = evt.LauncherActorId;
            var rootActorId = evt.RootActorId;
            var frame = evt.Frame;
            var pos = evt.Position;
            var reason = (int)evt.Reason;

            sink.Publish(ProjectileTriggering.Events.Exit, payload: evt, fillArgs: args =>
            {
                args[EffectTriggering.Args.Source] = source;
                args[EffectTriggering.Args.Target] = target;
                args[EffectTriggering.Args.OriginSource] = source;
                args[EffectTriggering.Args.OriginTarget] = target;

                args[ProjectileTriggering.Args.ProjectileId] = projectileId;
                args[ProjectileTriggering.Args.OwnerId] = ownerId;
                args[ProjectileTriggering.Args.TemplateId] = templateId;
                args[ProjectileTriggering.Args.LauncherActorId] = launcherActorId;
                args[ProjectileTriggering.Args.RootActorId] = rootActorId;
                args[ProjectileTriggering.Args.Frame] = frame;

                args[ProjectileTriggering.Args.ExitReason] = reason;
                args[ProjectileTriggering.Args.ExitPosition] = pos;
            });
        }
    }
}
