namespace AbilityKit.Ability.Triggering.Runtime
{
    public interface ITriggerRunningAction : ITriggerAction
    {
        IRunningAction Start(TriggerContext context);
    }

    public interface ITriggerActionV2 : ITriggerRunningAction
    {
    }
}
