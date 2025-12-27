namespace AbilityKit.Triggering.Runtime
{
    public interface ITriggerCondition
    {
        bool Evaluate(TriggerContext context);
    }
}
