using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Triggering.Variables.Numeric
{
    public sealed class NumericVarDomainRegistry : INumericVarDomainRegistry
    {
        private readonly Dictionary<string, INumericVarDomain> _domains;

        public NumericVarDomainRegistry()
        {
            _domains = new Dictionary<string, INumericVarDomain>(StringComparer.Ordinal);
        }

        public bool TryGetDomain(string domainId, out INumericVarDomain domain)
        {
            if (domainId == null)
            {
                domain = null;
                return false;
            }

            return _domains.TryGetValue(domainId, out domain);
        }

        public void Register(INumericVarDomain domain)
        {
            if (domain == null) throw new ArgumentNullException(nameof(domain));
            if (string.IsNullOrEmpty(domain.DomainId)) throw new ArgumentException("domain.DomainId is null or empty", nameof(domain));

            _domains[domain.DomainId] = domain;
        }
    }
}
