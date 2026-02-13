#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerPlanExportConditionHandlerRegistry
    {
        private static bool _initialized;
        private static ITriggerPlanExportConditionHandler[] _handlers;

        public static ITriggerPlanExportConditionHandler[] Handlers
        {
            get
            {
                EnsureInitialized();
                return _handlers;
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            var list = new List<(int order, ITriggerPlanExportConditionHandler handler)>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                    if (ex.LoaderExceptions != null)
                    {
                        for (int e = 0; e < ex.LoaderExceptions.Length; e++)
                        {
                            var le = ex.LoaderExceptions[e];
                            if (le == null) continue;
                            ExportLog.Exception(le, $"condition handler scan: asm.GetTypes failed. asm={asm.FullName}");
                        }
                    }
                    else
                    {
                        ExportLog.Exception(ex, $"condition handler scan: asm.GetTypes failed. asm={asm.FullName}");
                    }
                }
                if (types == null) continue;

                for (int i = 0; i < types.Length; i++)
                {
                    var t = types[i];
                    if (t == null) continue;
                    if (t.IsAbstract) continue;
                    if (!typeof(ITriggerPlanExportConditionHandler).IsAssignableFrom(t)) continue;

                    var attr = t.GetCustomAttribute<TriggerPlanExportHandlerAttribute>(false);
                    if (attr == null) continue;

                    try
                    {
                        if (Activator.CreateInstance(t) is ITriggerPlanExportConditionHandler h)
                        {
                            list.Add((attr.Order, h));
                        }
                    }
                    catch (Exception ex)
                    {
                        ExportLog.Exception(ex, $"create condition handler failed. type={t.FullName}");
                    }
                }
            }

            list.Sort(static (a, b) => a.order.CompareTo(b.order));

            _handlers = new ITriggerPlanExportConditionHandler[list.Count];
            for (int i = 0; i < list.Count; i++) _handlers[i] = list[i].handler;
        }
    }
}
#endif
