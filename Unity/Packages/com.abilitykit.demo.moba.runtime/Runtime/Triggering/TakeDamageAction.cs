using AbilityKit.Demo.Moba.Triggering.DamageActions;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Demo.Moba.Triggering
{
    public sealed class TakeDamageAction : ITriggerAction
    {
        private readonly DamageActionSpec _spec;

        public TakeDamageAction(DamageActionSpec spec)
        {
            _spec = spec ?? new DamageActionSpec();
        }

        public static TakeDamageAction FromDef(ActionDef def)
        {
            var spec = DamageActionSpecParser.ParseTakeDamage(def);
            return new TakeDamageAction(spec);
        }

        public void Execute(TriggerContext context)
        {
            TakeDamageExecutor.Execute(context, _spec);
        }

    }
}
