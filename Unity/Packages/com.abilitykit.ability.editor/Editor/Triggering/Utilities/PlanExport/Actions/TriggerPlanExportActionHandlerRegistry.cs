#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerPlanExportActionHandlerRegistry
    {
        private static bool _initialized;
        private static ITriggerPlanExportActionHandler[] _handlers;

        public static ITriggerPlanExportActionHandler[] Handlers
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

            var list = new List<(int order, ITriggerPlanExportActionHandler handler)>();

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
                            ExportLog.Exception(le, $"action handler scan: asm.GetTypes failed. asm={asm.FullName}");
                        }
                    }
                    else
                    {
                        ExportLog.Exception(ex, $"action handler scan: asm.GetTypes failed. asm={asm.FullName}");
                    }
                }
                if (types == null) continue;

                for (int i = 0; i < types.Length; i++)
                {
                    var t = types[i];
                    if (t == null) continue;
                    if (t.IsAbstract) continue;
                    if (!typeof(ITriggerPlanExportActionHandler).IsAssignableFrom(t)) continue;

                    var attr = t.GetCustomAttribute<TriggerPlanExportHandlerAttribute>(false);
                    if (attr == null) continue;

                    try
                    {
                        if (Activator.CreateInstance(t) is ITriggerPlanExportActionHandler h)
                        {
                            list.Add((attr.Order, h));
                        }
                    }
                    catch (Exception ex)
                    {
                        ExportLog.Exception(ex, $"create action handler failed. type={t.FullName}");
                    }
                }
            }

            list.Sort(static (a, b) => a.order.CompareTo(b.order));

            _handlers = new ITriggerPlanExportActionHandler[list.Count];
            for (int i = 0; i < list.Count; i++) _handlers[i] = list[i].handler;
        }
    }
}
#endif
