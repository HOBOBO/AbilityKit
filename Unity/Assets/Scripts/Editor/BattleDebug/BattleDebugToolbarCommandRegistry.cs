using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Game.Editor
{
    internal static class BattleDebugToolbarCommandRegistry
    {
        private static List<IBattleDebugToolbarCommand> _cache;

        public static IReadOnlyList<IBattleDebugToolbarCommand> GetAll()
        {
            if (_cache == null) Refresh();
            return _cache;
        }

        public static void Refresh()
        {
            _cache = new List<IBattleDebugToolbarCommand>();

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
                    if (!typeof(IBattleDebugToolbarCommand).IsAssignableFrom(t)) continue;

                    try
                    {
                        var inst = (IBattleDebugToolbarCommand)Activator.CreateInstance(t);
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
