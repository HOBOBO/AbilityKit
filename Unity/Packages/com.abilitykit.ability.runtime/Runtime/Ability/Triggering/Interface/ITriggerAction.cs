using AbilityKit.Ability.Triggering;

namespace AbilityKit.Ability.Triggering.Runtime
{
    public interface ITriggerAction
    {
        void Execute(TriggerContext context);
    }
}
