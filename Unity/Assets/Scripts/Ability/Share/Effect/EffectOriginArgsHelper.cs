using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba.EffectSource;

namespace AbilityKit.Ability.Share.Effect
{
    public static class EffectOriginArgsHelper
    {
        public static void FillFromRegistry(IDictionary<string, object> args, long sourceContextId, EffectSourceRegistry registry)
        {
            if (args == null) return;
            if (sourceContextId == 0) return;
            if (registry == null) return;

            try
            {
                if (registry.TryGetOrigin(sourceContextId, out var os, out var ot))
                {
                    args[EffectTriggering.Args.OriginSource] = os;
                    args[EffectTriggering.Args.OriginTarget] = ot;
                }

                if (registry.TryGetSnapshot(sourceContextId, out var snap))
                {
                    args[EffectTriggering.Args.OriginKind] = snap.Kind;
                    args[EffectTriggering.Args.OriginConfigId] = snap.ConfigId;
                    args[EffectTriggering.Args.OriginContextId] = snap.ContextId;
                }
            }
            catch
            {
            }
        }

        public static void FillFromServices(IDictionary<string, object> args, long sourceContextId, IServiceProvider services)
        {
            if (args == null) return;
            if (sourceContextId == 0) return;
            if (services == null) return;

            try
            {
                var regObj = services.GetService(typeof(EffectSourceRegistry));
                if (regObj is EffectSourceRegistry reg)
                {
                    FillFromRegistry(args, sourceContextId, reg);
                }
            }
            catch
            {
            }
        }
    }
}
