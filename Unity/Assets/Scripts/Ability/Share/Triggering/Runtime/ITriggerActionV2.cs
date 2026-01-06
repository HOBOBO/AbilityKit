namespace AbilityKit.Ability.Triggering.Runtime
{
    public interface ITriggerActionV2 : ITriggerAction
    {
        IRunningAction Start(TriggerContext context);
    }
}
