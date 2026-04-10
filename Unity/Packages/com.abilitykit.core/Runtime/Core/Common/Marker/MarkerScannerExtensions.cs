using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AbilityKit.Core.Common.Marker
{
    /// <summary>
    /// MarkerScanner 的扩展方法，提供更便捷的扫描功能。
    /// </summary>
    public static class MarkerScannerExtensions
    {
        /// <summary>
        /// 扫描所有程序集，排除系统程序集以提高性能。
        /// </summary>
        /// <typeparam name="TAttr">MarkerAttribute 子类</typeparam>
        /// <param name="registry">目标注册表</param>
        /// <param name="excludeSystemAssemblies">是否排除系统程序集（默认 true）</param>
        public static void ScanAllExcludingSystem<TAttr>(this IMarkerRegistry registry, bool excludeSystemAssemblies = true)
            where TAttr : MarkerAttribute
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            if (excludeSystemAssemblies)
            {
                assemblies = FilterOutSystemAssemblies(assemblies);
            }

            MarkerScanner<TAttr>.Scan(assemblies, registry);
        }

        /// <summary>
        /// 扫描指定程序集及其引用程序集。
        /// </summary>
        /// <typeparam name="TAttr">MarkerAttribute 子类</typeparam>
        /// <param name="startAssembly">起始程序集</param>
        /// <param name="registry">目标注册表</param>
        /// <param name="maxDepth">最大递归深度（默认 2）</param>
        public static void ScanWithReferences<TAttr>(this IMarkerRegistry registry, Assembly startAssembly, int maxDepth = 2)
            where TAttr : MarkerAttribute
        {
            if (startAssembly == null)
                return;

            var visited = new HashSet<string>();
            var toScan = new List<Assembly>();
            CollectReferencedAssemblies(startAssembly, visited, toScan, maxDepth);

            MarkerScanner<TAttr>.Scan(toScan.ToArray(), registry);
        }

        /// <summary>
        /// 扫描程序集，排除指定的程序集名称。
        /// </summary>
        /// <typeparam name="TAttr">MarkerAttribute 子类</typeparam>
        /// <param name="registry">目标注册表</param>
        /// <param name="excludeAssemblyNames">要排除的程序集名称（不含 .dll 后缀）</param>
        public static void ScanExcluding<TAttr>(this IMarkerRegistry registry, params string[] excludeAssemblyNames)
            where TAttr : MarkerAttribute
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            if (excludeAssemblyNames != null && excludeAssemblyNames.Length > 0)
            {
                var excludeSet = new HashSet<string>(excludeAssemblyNames, StringComparer.OrdinalIgnoreCase);
                assemblies = Array.FindAll(assemblies, asm =>
                {
                    var name = asm.GetName().Name ?? "";
                    return !excludeSet.Contains(name);
                });
            }

            MarkerScanner<TAttr>.Scan(assemblies, registry);
        }

        /// <summary>
        /// 扫描指定命名空间下的所有类型。
        /// </summary>
        /// <typeparam name="TAttr">MarkerAttribute 子类</typeparam>
        /// <param name="registry">目标注册表</param>
        /// <param name="namespacePrefix">命名空间前缀（如 "AbilityKit.Ability" 会匹配所有以该前缀开头的命名空间）</param>
        public static void ScanByNamespace<TAttr>(this IMarkerRegistry registry, string namespacePrefix)
            where TAttr : MarkerAttribute
        {
            ScanByNamespace<TAttr>(registry, new[] { namespacePrefix });
        }

        /// <summary>
        /// 扫描指定命名空间下的所有类型。
        /// </summary>
        /// <typeparam name="TAttr">MarkerAttribute 子类</typeparam>
        /// <param name="registry">目标注册表</param>
        /// <param name="namespacePrefixes">多个命名空间前缀</param>
        public static void ScanByNamespace<TAttr>(this IMarkerRegistry registry, string[] namespacePrefixes)
            where TAttr : MarkerAttribute
        {
            if (string.IsNullOrEmpty(namespacePrefixes?.FirstOrDefault()))
                return;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var namespaceSet = new HashSet<string>(namespacePrefixes, StringComparer.OrdinalIgnoreCase);

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
                    types = e.Types ?? Array.Empty<Type>();
                }

                if (types == null) continue;

                for (int i = 0; i < types.Length; i++)
                {
                    var t = types[i];
                    if (t == null) continue;
                    if (t.Namespace == null) continue;
                    if (t.IsAbstract || t.IsInterface) continue;

                    bool matches = false;
                    foreach (var prefix in namespacePrefixes)
                    {
                        if (t.Namespace.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            matches = true;
                            break;
                        }
                    }

                    if (!matches) continue;

                    var attrs = t.GetCustomAttributes(typeof(TAttr), false);
                    if (attrs == null || attrs.Length == 0) continue;

                    var attr = (TAttr)attrs[0];
                    attr.OnScanned(t, registry);
                }
            }
        }

        /// <summary>
        /// 获取所有已加载的程序集中名称包含指定关键字的程序集。
        /// </summary>
        public static Assembly[] GetAssembliesByName(string nameContains)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(asm =>
                {
                    var name = asm.GetName().Name ?? "";
                    return name.Contains(nameContains, StringComparison.OrdinalIgnoreCase);
                })
                .ToArray();
        }

        /// <summary>
        /// 过滤掉系统程序集（Unity、.NET 系统库等）。
        /// </summary>
        private static Assembly[] FilterOutSystemAssemblies(Assembly[] assemblies)
        {
            return Array.FindAll(assemblies, asm =>
            {
                var name = asm.GetName().Name ?? "";
                // 排除常见的系统程序集
                if (name.StartsWith("System.") ||
                    name.StartsWith("Unity.") ||
                    name.StartsWith("UnityEngine.") ||
                    name.StartsWith("UnityEditor.") ||
                    name == "mscorlib" ||
                    name == "netstandard" ||
                    name == "Microsoft" ||
                    name.StartsWith("nunit.") ||
                    name.EndsWith(".Resources") ||
                    name == "Mono.Security")
                {
                    return false;
                }
                return true;
            });
        }

        /// <summary>
        /// 递归收集引用程序集。
        /// </summary>
        private static void CollectReferencedAssemblies(Assembly assembly, HashSet<string> visited,
            List<Assembly> result, int remainingDepth)
        {
            if (remainingDepth < 0)
                return;

            var name = assembly.GetName().Name;
            if (string.IsNullOrEmpty(name))
                return;

            if (!visited.Add(name))
                return;

            result.Add(assembly);

            try
            {
                foreach (var refAsmName in assembly.GetReferencedAssemblies())
                {
                    try
                    {
                        var refAsm = Assembly.Load(refAsmName);
                        CollectReferencedAssemblies(refAsm, visited, result, remainingDepth - 1);
                    }
                    catch
                    {
                        // 忽略无法加载的程序集
                    }
                }
            }
            catch
            {
                // 忽略获取引用程序集失败的异常
            }
        }
    }
}
