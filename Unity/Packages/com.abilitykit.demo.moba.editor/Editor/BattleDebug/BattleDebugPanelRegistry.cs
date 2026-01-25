using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Game.Editor
{
    internal static class BattleDebugPanelRegistry
    {
        private static List<IBattleDebugPanel> _cache;

        public static IReadOnlyList<IBattleDebugPanel> GetAll()
        {
            if (_cache == null) Refresh();
            return _cache;
        }

        public static void Refresh()
        {
            _cache = new List<IBattleDebugPanel>();

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
                    if (!typeof(IBattleDebugPanel).IsAssignableFrom(t)) continue;

                    try
                    {
                        var inst = (IBattleDebugPanel)Activator.CreateInstance(t);
                        if (inst != null) _cache.Add(inst);
                    }
                    catch
                    {
                    }
                }
            }

            _cache.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
    }
}
