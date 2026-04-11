using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Examples.Triggering
{
    public sealed class TriggerContextFactory : ITriggerContextFactory
    {
        private readonly IServiceProvider _services;

        public TriggerContextFactory(IServiceProvider services = null)
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
