using System;
using System.Collections.Generic;
using AbilityKit.Effect;

namespace AbilityKit.Ability.Triggering
{
    using AbilityKit.Ability.Share.Effect;
    public static class TriggerEventPublishExtensions
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

                    if (TryGet(parentArgs, EffectTriggering.Args.OriginKind, out var originKind)) args[EffectTriggering.Args.OriginKind] = originKind;
                    if (TryGet(parentArgs, EffectTriggering.Args.OriginConfigId, out var originConfigId)) args[EffectTriggering.Args.OriginConfigId] = originConfigId;
                    if (TryGet(parentArgs, EffectTriggering.Args.OriginContextId, out var originContextId)) args[EffectTriggering.Args.OriginContextId] = originContextId;

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
