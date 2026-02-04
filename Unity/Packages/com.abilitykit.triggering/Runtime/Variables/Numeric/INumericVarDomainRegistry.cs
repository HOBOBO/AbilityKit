namespace AbilityKit.Triggering.Variables.Numeric
{
    public interface INumericVarDomainRegistry
    {
        bool TryGetDomain(string domainId, out INumericVarDomain domain);
    }
}
