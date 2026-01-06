using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Triggering.Runtime
{
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
                args.TryGetValue("source", out source);
                args.TryGetValue("target", out target);
            }

            var ctx = TriggerContext.Rent();
            ctx.Init(_services, source, target, sharedLocalVars);
            return ctx;
        }
    }
}
