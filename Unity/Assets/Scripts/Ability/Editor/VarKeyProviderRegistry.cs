using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Ability.Editor
{
    internal static class VarKeyProviderRegistry
    {
        private static List<IVarKeyProvider> _cache;

        public static IReadOnlyList<IVarKeyProvider> GetAll()
        {
            if (_cache == null) Refresh();
            return _cache;
        }

        public static void Refresh()
        {
            _cache = new List<IVarKeyProvider>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null) continue;
                for (int j = 0; j < types.Length; j++)
                {
                    var t = types[j];
                    if (t == null || t.IsAbstract) continue;
                    if (!typeof(IVarKeyProvider).IsAssignableFrom(t)) continue;

                    try
                    {
                        var inst = (IVarKeyProvider)Activator.CreateInstance(t);
                        if (inst != null) _cache.Add(inst);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            _cache.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        public static IEnumerable<string> GetKeys(in VarKeyQuery query)
        {
            var list = new List<string>();
            var providers = GetAll();
            for (int i = 0; i < providers.Count; i++)
            {
                var p = providers[i];
                if (p == null) continue;
                if (!p.CanProvide(in query)) continue;
                p.CollectKeys(in query, list);
            }

            if (list.Count <= 1) return list;

            list.Sort(StringComparer.Ordinal);
            for (int i = list.Count - 1; i > 0; i--)
            {
                if (string.Equals(list[i], list[i - 1], StringComparison.Ordinal))
                {
                    list.RemoveAt(i);
                }
            }

            return list;
        }
    }
}
