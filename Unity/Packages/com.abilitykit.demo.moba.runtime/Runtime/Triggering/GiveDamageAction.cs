using System;
using AbilityKit.Demo.Moba.Triggering.DamageActions;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Demo.Moba.Triggering
{
    public sealed class GiveDamageAction : ITriggerAction
    {
        private readonly DamageActionSpec _spec;

        public GiveDamageAction(DamageActionSpec spec)
        {
            _spec = spec ?? new DamageActionSpec();
        }

        public static GiveDamageAction FromDef(ActionDef def)
        {
            var spec = DamageActionSpecParser.ParseGiveDamage(def);
            return new GiveDamageAction(spec);
        }

        public void Execute(TriggerContext context)
        {
            GiveDamageExecutor.Execute(context, _spec);
        }
    }
}
