namespace AbilityKit.Triggering.Variables.Numeric
{
    public sealed class DefaultNumericVarDomainRegistry : INumericVarDomainRegistry
    {
        public static readonly DefaultNumericVarDomainRegistry Instance = new DefaultNumericVarDomainRegistry();

        private readonly NumericVarDomainRegistry _inner;

        private DefaultNumericVarDomainRegistry()
        {
            _inner = new NumericVarDomainRegistry();
        }

        public bool TryGetDomain(string domainId, out INumericVarDomain domain)
        {
            return _inner.TryGetDomain(domainId, out domain);
        }

        public void Register(INumericVarDomain domain)
        {
            _inner.Register(domain);
        }
    }
}
