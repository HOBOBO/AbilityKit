using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Ability.Editor
{
    internal static class AbilityListToolbarCommandRegistry
    {
        private static List<IAbilityListToolbarCommand> _cache;

        public static IReadOnlyList<IAbilityListToolbarCommand> GetAll()
        {
            if (_cache == null) Refresh();
            return _cache;
        }

        public static void Refresh()
        {
            _cache = new List<IAbilityListToolbarCommand>();

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
                    if (!typeof(IAbilityListToolbarCommand).IsAssignableFrom(t)) continue;

                    try
                    {
                        var inst = (IAbilityListToolbarCommand)Activator.CreateInstance(t);
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
    }
}
