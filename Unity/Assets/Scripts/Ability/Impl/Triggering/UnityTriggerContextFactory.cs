using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Impl.Triggering
{
    public sealed class UnityTriggerContextFactory : ITriggerContextFactory
    {
        private readonly IServiceProvider _services;

        public UnityTriggerContextFactory(IServiceProvider services = null)
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

            return new TriggerContext(_services, source, target, sharedLocalVars);
        }
    }
}
