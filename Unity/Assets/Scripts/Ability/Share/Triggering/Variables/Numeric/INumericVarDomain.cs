namespace AbilityKit.Ability.Triggering.Variables.Numeric
{
    public interface INumericVarDomain
    {
        string DomainId { get; }

        bool TryGet(TriggerContext context, string key, out double value);

        bool TrySet(TriggerContext context, string key, double value);
    }
}
