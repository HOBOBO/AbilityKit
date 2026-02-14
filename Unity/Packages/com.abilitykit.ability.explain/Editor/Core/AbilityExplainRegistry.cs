using System.Collections.Generic;
using AbilityKit.Ability.Explain;

namespace AbilityKit.Ability.Explain.Editor
{
    public static class AbilityExplainRegistry
    {
        private static readonly List<IEntityProvider> EntityProviders = new List<IEntityProvider>();
        private static readonly List<IExplainResolver> Resolvers = new List<IExplainResolver>();
        private static readonly List<INavigator> Navigators = new List<INavigator>();
        private static readonly List<IExplainDetailsSectionProvider> DetailsSectionProviders = new List<IExplainDetailsSectionProvider>();
        private static readonly List<IDiscoveryPolicy> DiscoveryPolicies = new List<IDiscoveryPolicy>();
        private static readonly List<IExplainEntityListModule> EntityListModules = new List<IExplainEntityListModule>();
        private static readonly List<IExplainNodeContextMenuProvider> NodeContextMenuProviders = new List<IExplainNodeContextMenuProvider>();

        public static void Register(IEntityProvider provider)
        {
            if (provider == null) return;
            if (!EntityProviders.Contains(provider)) EntityProviders.Add(provider);
        }

        public static void Register(IExplainResolver resolver)
        {
            if (resolver == null) return;
            if (!Resolvers.Contains(resolver)) Resolvers.Add(resolver);
        }

        public static void Register(INavigator navigator)
        {
            if (navigator == null) return;
            if (!Navigators.Contains(navigator)) Navigators.Add(navigator);
        }

        public static void Register(IExplainDetailsSectionProvider provider)
        {
            if (provider == null) return;
            if (!DetailsSectionProviders.Contains(provider)) DetailsSectionProviders.Add(provider);
        }

        public static void Register(IDiscoveryPolicy policy)
        {
            if (policy == null) return;
            if (!DiscoveryPolicies.Contains(policy)) DiscoveryPolicies.Add(policy);
        }

        public static void Register(IExplainEntityListModule module)
        {
            if (module == null) return;
            if (!EntityListModules.Contains(module)) EntityListModules.Add(module);
        }

        public static void Register(IExplainNodeContextMenuProvider provider)
        {
            if (provider == null) return;
            if (!NodeContextMenuProviders.Contains(provider)) NodeContextMenuProviders.Add(provider);
        }

        public static IEntityProvider GetEntityProvider()
        {
            return GetEntityProvider(null);
        }

        public static IEntityProvider GetEntityProvider(string searchText)
        {
            if (EntityProviders.Count <= 0) return null;

            IEntityProvider best = null;
            var bestPriority = int.MinValue;

            for (var i = 0; i < EntityProviders.Count; i++)
            {
                var p = EntityProviders[i];
                if (p == null) continue;

                if (p is IEntityProviderEx ex && !ex.CanProvide(searchText)) continue;

                var pri = GetPriority(p);
                if (best == null || pri > bestPriority)
                {
                    best = p;
                    bestPriority = pri;
                }
            }

            return best;
        }

        public static List<IExplainNodeContextMenuProvider> GetNodeContextMenuProviders(ExplainNode node, ExplainNodeContextMenuContext context)
        {
            if (NodeContextMenuProviders.Count <= 0) return null;

            List<IExplainNodeContextMenuProvider> result = null;
            for (var i = 0; i < NodeContextMenuProviders.Count; i++)
            {
                var p = NodeContextMenuProviders[i];
                if (p == null) continue;
                if (!p.CanProvide(node, context)) continue;
                if (result == null) result = new List<IExplainNodeContextMenuProvider>();
                result.Add(p);
            }

            if (result == null || result.Count <= 1) return result;
            result.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            return result;
        }

        public static IExplainEntityListModule GetEntityListModule(IEntityProvider provider)
        {
            if (EntityListModules.Count <= 0) return null;

            IExplainEntityListModule best = null;
            var bestPriority = int.MinValue;

            for (var i = 0; i < EntityListModules.Count; i++)
            {
                var m = EntityListModules[i];
                if (m == null) continue;
                if (!m.CanHandle(provider)) continue;

                var pri = GetPriority(m);
                if (best == null || pri > bestPriority)
                {
                    best = m;
                    bestPriority = pri;
                }
            }

            return best;
        }

        public static IExplainResolver GetResolver()
        {
            return GetResolver(null);
        }

        public static IExplainResolver GetResolver(ExplainResolveRequest request)
        {
            if (Resolvers.Count <= 0) return null;

            IExplainResolver best = null;
            var bestPriority = int.MinValue;

            for (var i = 0; i < Resolvers.Count; i++)
            {
                var r = Resolvers[i];
                if (r == null) continue;

                if (r is IExplainResolverEx ex && request != null && !ex.CanResolve(request)) continue;

                var pri = GetPriority(r);
                if (best == null || pri > bestPriority)
                {
                    best = r;
                    bestPriority = pri;
                }
            }

            return best;
        }

        public static IExplainResolver GetResolverForExpand(ExplainExpandRequest request)
        {
            if (Resolvers.Count <= 0) return null;

            IExplainResolver best = null;
            var bestPriority = int.MinValue;

            for (var i = 0; i < Resolvers.Count; i++)
            {
                var r = Resolvers[i];
                if (r == null) continue;

                if (r is IExplainResolverEx ex && request != null && !ex.CanExpand(request)) continue;

                var pri = GetPriority(r);
                if (best == null || pri > bestPriority)
                {
                    best = r;
                    bestPriority = pri;
                }
            }

            return best;
        }

        public static INavigator GetNavigator()
        {
            return GetNavigator(null);
        }

        public static INavigator GetNavigator(NavigationTarget target)
        {
            if (Navigators.Count <= 0) return null;

            INavigator best = null;
            var bestPriority = int.MinValue;

            for (var i = 0; i < Navigators.Count; i++)
            {
                var n = Navigators[i];
                if (n == null) continue;

                if (n is INavigatorEx ex && target != null && !ex.CanNavigateExt(target)) continue;
                if (target != null && !n.CanNavigate(target)) continue;

                var pri = GetPriority(n);
                if (best == null || pri > bestPriority)
                {
                    best = n;
                    bestPriority = pri;
                }
            }

            return best;
        }

        private static int GetPriority(object o)
        {
            return o is IRegistryPriority p ? p.Priority : 0;
        }

        public static List<IExplainDetailsSectionProvider> GetDetailsSectionProviders(ExplainNode node, ExplainDetailsContext context)
        {
            if (DetailsSectionProviders.Count <= 0) return null;

            List<IExplainDetailsSectionProvider> result = null;
            for (var i = 0; i < DetailsSectionProviders.Count; i++)
            {
                var p = DetailsSectionProviders[i];
                if (p == null) continue;
                if (!p.CanProvide(node, context)) continue;
                if (result == null) result = new List<IExplainDetailsSectionProvider>();
                result.Add(p);
            }

            if (result == null || result.Count <= 1) return result;

            result.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            return result;
        }

        public static IDiscoveryPolicy GetDiscoveryPolicy()
        {
            if (DiscoveryPolicies.Count <= 0) return null;

            IDiscoveryPolicy best = null;
            var bestPriority = int.MinValue;

            for (var i = 0; i < DiscoveryPolicies.Count; i++)
            {
                var p = DiscoveryPolicies[i];
                if (p == null) continue;

                var pri = GetPriority(p);
                if (best == null || pri > bestPriority)
                {
                    best = p;
                    bestPriority = pri;
                }
            }

            return best;
        }

        public static void ClearAll()
        {
            EntityProviders.Clear();
            Resolvers.Clear();
            Navigators.Clear();
            DetailsSectionProviders.Clear();
            DiscoveryPolicies.Clear();
            EntityListModules.Clear();
            NodeContextMenuProviders.Clear();
        }
    }
}
