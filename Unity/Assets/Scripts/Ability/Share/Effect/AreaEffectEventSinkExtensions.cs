using AbilityKit.Ability.Share.Common.Projectile;

namespace AbilityKit.Ability.Share.Effect
{
    public static class AreaEffectEventSinkExtensions
    {
        public static void PublishAreaSpawn(this IEffectEventSink sink, in AreaSpawnEvent evt, object source = null, object target = null)
        {
            if (sink == null) return;

            var areaId = evt.Area.Value;
            var ownerId = evt.OwnerId;
            var frame = evt.Frame;
            var center = evt.Center;
            var radius = evt.Radius;

            sink.Publish(AreaTriggering.Events.Spawn, payload: null, fillArgs: args =>
            {
                args[EffectTriggering.Args.Source] = source;
                args[EffectTriggering.Args.Target] = target;

                args[AreaTriggering.Args.AreaId] = areaId;
                args[AreaTriggering.Args.OwnerId] = ownerId;
                args[AreaTriggering.Args.Frame] = frame;
                args[AreaTriggering.Args.Center] = center;
                args[AreaTriggering.Args.Radius] = radius;
            });
        }

        public static void PublishAreaEnter(this IEffectEventSink sink, in AreaEnterEvent evt, object source = null, object target = null)
        {
            if (sink == null) return;

            var areaId = evt.Area.Value;
            var ownerId = evt.OwnerId;
            var collider = evt.Collider;
            var frame = evt.Frame;

            sink.Publish(AreaTriggering.Events.Enter, payload: null, fillArgs: args =>
            {
                args[EffectTriggering.Args.Source] = source;
                args[EffectTriggering.Args.Target] = target;

                args[AreaTriggering.Args.AreaId] = areaId;
                args[AreaTriggering.Args.OwnerId] = ownerId;
                args[AreaTriggering.Args.Frame] = frame;
                args[AreaTriggering.Args.Collider] = collider;
            });
        }

        public static void PublishAreaStay(this IEffectEventSink sink, in AreaStayEvent evt, object source = null, object target = null)
        {
            if (sink == null) return;

            var areaId = evt.Area.Value;
            var ownerId = evt.OwnerId;
            var collider = evt.Collider;
            var frame = evt.Frame;

            sink.Publish(AreaTriggering.Events.Stay, payload: null, fillArgs: args =>
            {
                args[EffectTriggering.Args.Source] = source;
                args[EffectTriggering.Args.Target] = target;

                args[AreaTriggering.Args.AreaId] = areaId;
                args[AreaTriggering.Args.OwnerId] = ownerId;
                args[AreaTriggering.Args.Frame] = frame;
                args[AreaTriggering.Args.Collider] = collider;
            });
        }

        public static void PublishAreaExit(this IEffectEventSink sink, in AreaExitEvent evt, object source = null, object target = null)
        {
            if (sink == null) return;

            var areaId = evt.Area.Value;
            var ownerId = evt.OwnerId;
            var collider = evt.Collider;
            var frame = evt.Frame;

            sink.Publish(AreaTriggering.Events.Exit, payload: null, fillArgs: args =>
            {
                args[EffectTriggering.Args.Source] = source;
                args[EffectTriggering.Args.Target] = target;

                args[AreaTriggering.Args.AreaId] = areaId;
                args[AreaTriggering.Args.OwnerId] = ownerId;
                args[AreaTriggering.Args.Frame] = frame;
                args[AreaTriggering.Args.Collider] = collider;
            });
        }

        public static void PublishAreaExpire(this IEffectEventSink sink, in AreaExpireEvent evt, object source = null, object target = null)
        {
            if (sink == null) return;

            var areaId = evt.Area.Value;
            var ownerId = evt.OwnerId;
            var frame = evt.Frame;

            sink.Publish(AreaTriggering.Events.Expire, payload: null, fillArgs: args =>
            {
                args[EffectTriggering.Args.Source] = source;
                args[EffectTriggering.Args.Target] = target;

                args[AreaTriggering.Args.AreaId] = areaId;
                args[AreaTriggering.Args.OwnerId] = ownerId;
                args[AreaTriggering.Args.Frame] = frame;
            });
        }
    }
}
