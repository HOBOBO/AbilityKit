using AbilityKit.Triggering;

namespace AbilityKit.Triggering.Runtime
{
    public interface ITriggerAction
    {
        void Execute(TriggerContext context);
    }
}
