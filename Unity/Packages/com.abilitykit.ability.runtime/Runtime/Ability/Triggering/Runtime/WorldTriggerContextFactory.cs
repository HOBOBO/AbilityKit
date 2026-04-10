using System;
using System.Collections.Generic;
using AbilityKit.Effect;

namespace AbilityKit.Ability.Triggering.Runtime
{
    using AbilityKit.Ability.Share.Effect;
    public sealed class WorldTriggerContextFactory : ITriggerContextFactory
    {
        private readonly IServiceProvider _services;

        public WorldTriggerContextFactory(IServiceProvider services)
        {
            _services = services;
        }

        public TriggerContext CreateContext(in TriggerEvent evt, Dictionary<string, object> sharedLocalVars)
        {
            object source = null;
            object target = null;

            var args = evt.Args;
            if (args != null)
            {
                args.TryGetValue(EffectTriggering.Args.Source, out source);
                args.TryGetValue(EffectTriggering.Args.Target, out target);

                if (args is IDictionary<string, object> dict)
                {
                    if (!dict.ContainsKey(EffectTriggering.Args.OriginSource)) dict[EffectTriggering.Args.OriginSource] = source;
                    if (!dict.ContainsKey(EffectTriggering.Args.OriginTarget)) dict[EffectTriggering.Args.OriginTarget] = target;
                }
            }

            var ctx = TriggerContext.Rent();
            ctx.Init(_services, source, target, sharedLocalVars);
            return ctx;
        }
    }
}
