namespace AbilityKit.Ability.Triggering.Runtime
{
    public interface ITriggerCondition
    {
        bool Evaluate(TriggerContext context);
    }
}
