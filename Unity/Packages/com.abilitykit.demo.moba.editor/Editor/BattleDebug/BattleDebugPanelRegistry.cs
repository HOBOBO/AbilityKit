using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Game.Editor
{
    internal static class BattleDebugPanelRegistry
    {
        private const int MaxLoadErrors = 8;
        private static List<IBattleDebugPanel> _cache;
        private static List<string> _loadErrors;

        public static IReadOnlyList<string> LoadErrors
        {
            get
            {
                if (_cache == null) Refresh();
                return _loadErrors;
            }
        }

        public static IReadOnlyList<IBattleDebugPanel> GetAll()
        {
            if (_cache == null) Refresh();
            return _cache;
        }

        public static void Refresh()
        {
            _cache = new List<IBattleDebugPanel>();
            _loadErrors = new List<string>();

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
                    catch (Exception ex)
                    {
                        if (_loadErrors.Count >= MaxLoadErrors) continue;
                        var root = ex is TargetInvocationException invocation && invocation.InnerException != null
                            ? invocation.InnerException
                            : ex;
                        _loadErrors.Add(t.FullName + ": " + root.GetType().Name + ": " + root.Message);
                    }
                }
            }

            _cache.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
    }
}
