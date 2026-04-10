using System.Collections.Generic;
using AbilityKit.Battle.SearchTarget;

namespace AbilityKit.Battle.SearchTarget.Providers
{
    public sealed class ConcatCandidateProvider : ICandidateProvider
    {
        private readonly IReadOnlyList<ICandidateProvider> _providers;

        public ConcatCandidateProvider(IReadOnlyList<ICandidateProvider> providers)
        {
            _providers = providers;
        }

        public bool RequiresPosition
        {
            get
            {
                if (_providers == null) return false;
                for (int i = 0; i < _providers.Count; i++)
                {
                    var p = _providers[i];
                    if (p != null && p.RequiresPosition) return true;
                }
                return false;
            }
        }

        public void ForEachCandidate<TConsumer>(in SearchQuery query, SearchContext context, ref TConsumer consumer)
            where TConsumer : struct, ICandidateConsumer
        {
            if (_providers == null || _providers.Count == 0) return;

            for (int i = 0; i < _providers.Count; i++)
            {
                var p = _providers[i];
                if (p == null) continue;
                p.ForEachCandidate(in query, context, ref consumer);
            }
        }
    }
}
