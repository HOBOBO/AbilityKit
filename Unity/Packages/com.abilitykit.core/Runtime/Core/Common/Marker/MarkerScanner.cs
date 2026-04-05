using System;
using System.Reflection;

namespace AbilityKit.Common.Marker
{
    /// <summary>
    /// 标记系统扫描器。
    /// 扫描程序集中标记了 TAttr 的类型，调用其 OnScanned 方法。
    /// </summary>
    /// <typeparam name="TAttr">自定义的 MarkerAttribute 子类</typeparam>
    public static class MarkerScanner<TAttr> where TAttr : MarkerAttribute
    {
        /// <summary>
        /// 扫描所有已加载的程序集。
        /// </summary>
        /// <param name="registry">目标注册表</param>
        public static void ScanAll(IMarkerRegistry registry)
        {
            Scan(AppDomain.CurrentDomain.GetAssemblies(), registry);
        }

        /// <summary>
        /// 扫描指定的程序集。
        /// </summary>
        /// <param name="assemblies">目标程序集数组</param>
        /// <param name="registry">目标注册表</param>
        public static void Scan(Assembly[] assemblies, IMarkerRegistry registry)
        {
            if (registry == null) return;
            if (assemblies == null || assemblies.Length == 0) return;

            for (int a = 0; a < assemblies.Length; a++)
            {
                var asm = assemblies[a];
                if (asm == null) continue;

                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                if (types == null) continue;

                for (int i = 0; i < types.Length; i++)
                {
                    var t = types[i];
                    if (t == null) continue;
                    if (t.IsAbstract) continue;
                    if (t.IsInterface) continue;

                    var attr = GetAttribute(t);
                    if (attr == null) continue;

                    attr.OnScanned(t, registry);
                }
            }
        }

        private static TAttr? GetAttribute(Type type)
        {
            var attrs = type.GetCustomAttributes(typeof(TAttr), false);
            if (attrs == null || attrs.Length == 0) return null;
            return (TAttr)attrs[0];
        }
    }
}
