namespace AbilityKit.Ability.Triggering.Runtime
{
    public interface ITriggerContextFactory
    {
        TriggerContext CreateContext(in TriggerEvent evt, System.Collections.Generic.Dictionary<string, object> sharedLocalVars);
    }
}
