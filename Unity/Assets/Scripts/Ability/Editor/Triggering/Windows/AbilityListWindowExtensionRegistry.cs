using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Ability.Editor
{
    internal static class AbilityListWindowExtensionRegistry
    {
        private static List<IAbilityListWindowExtension> _cache;

        public static IReadOnlyList<IAbilityListWindowExtension> GetAll()
        {
            if (_cache == null) Refresh();
            return _cache;
        }

        public static void Refresh()
        {
            _cache = new List<IAbilityListWindowExtension>();

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
                    if (!typeof(IAbilityListWindowExtension).IsAssignableFrom(t)) continue;

                    try
                    {
                        var inst = (IAbilityListWindowExtension)Activator.CreateInstance(t);
                        if (inst != null) _cache.Add(inst);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }
    }
}
