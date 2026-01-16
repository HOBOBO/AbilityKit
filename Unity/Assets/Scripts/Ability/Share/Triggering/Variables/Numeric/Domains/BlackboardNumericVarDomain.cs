using System;
using AbilityKit.Ability.Triggering.Blackboard;

namespace AbilityKit.Ability.Triggering.Variables.Numeric.Domains
{
    public sealed class BlackboardNumericVarDomain : INumericVarDomain
    {
        private readonly string _domainId;
        private readonly string _boardId;

        public BlackboardNumericVarDomain(string domainId, string boardId)
        {
            if (string.IsNullOrEmpty(domainId)) throw new ArgumentNullException(nameof(domainId));
            if (string.IsNullOrEmpty(boardId)) throw new ArgumentNullException(nameof(boardId));

            _domainId = domainId;
            _boardId = boardId;
        }

        public string DomainId => _domainId;

        public bool TryGet(TriggerContext context, string key, out double value)
        {
            value = 0d;
            if (context == null || string.IsNullOrEmpty(key)) return false;

            if (!context.TryResolveBlackboard(_boardId, out var bb) || bb == null) return false;

            return bb.TryGetDouble(key, out value);
        }

        public bool TrySet(TriggerContext context, string key, double value)
        {
            if (context == null || string.IsNullOrEmpty(key)) return false;

            if (!context.TryResolveBlackboard(_boardId, out var bb) || bb == null) return false;

            bb.Set(key, value);
            return true;
        }
    }
}
