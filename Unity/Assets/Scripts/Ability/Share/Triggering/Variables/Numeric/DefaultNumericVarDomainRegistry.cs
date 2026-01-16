using AbilityKit.Ability.Triggering.Variables.Numeric.Domains;

namespace AbilityKit.Ability.Triggering.Variables.Numeric
{
    public sealed class DefaultNumericVarDomainRegistry : INumericVarDomainRegistry
    {
        public static readonly DefaultNumericVarDomainRegistry Instance = new DefaultNumericVarDomainRegistry();

        private readonly NumericVarDomainRegistry _inner;

        private DefaultNumericVarDomainRegistry()
        {
            _inner = new NumericVarDomainRegistry();
            _inner.Register(new LocalNumericVarDomain());
            _inner.Register(new GlobalNumericVarDomain());
        }

        public bool TryGetDomain(string domainId, out INumericVarDomain domain)
        {
            return _inner.TryGetDomain(domainId, out domain);
        }
    }
}
