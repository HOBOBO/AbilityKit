using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Effect;

namespace AbilityKit.Ability.Triggering
{
    public static class TriggerEventPublishExtensions_SharedTriggering
    {
        public static void PublishInherited(this IEventBus bus, string eventId, object payload, IReadOnlyDictionary<string, object> parentArgs, Action<PooledTriggerArgs> fillArgs = null)
        {
            if (bus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            var args = PooledTriggerArgs.Rent();
            try
            {
                if (parentArgs != null)
                {
                    if (TryGet(parentArgs, EffectTriggering.Args.OriginSource, out var originSource)) args[EffectTriggering.Args.OriginSource] = originSource;
                    if (TryGet(parentArgs, EffectTriggering.Args.OriginTarget, out var originTarget)) args[EffectTriggering.Args.OriginTarget] = originTarget;

                    if (!args.ContainsKey(EffectTriggering.Args.OriginSource) && TryGet(parentArgs, EffectTriggering.Args.Source, out var source)) args[EffectTriggering.Args.OriginSource] = source;
                    if (!args.ContainsKey(EffectTriggering.Args.OriginTarget) && TryGet(parentArgs, EffectTriggering.Args.Target, out var target)) args[EffectTriggering.Args.OriginTarget] = target;
                }

                fillArgs?.Invoke(args);
                bus.Publish(new TriggerEvent(eventId, payload, args));
            }
            catch
            {
                args.Dispose();
                throw;
            }
        }

        private static bool TryGet(IReadOnlyDictionary<string, object> args, string key, out object value)
        {
            if (args != null && key != null && args.TryGetValue(key, out value))
            {
                return true;
            }

            value = null;
            return false;
        }
    }
}
