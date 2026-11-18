using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AbilityKit.Core.Markers
{
    /// <summary>
    /// 保留程序集及命名空间发现的兼容辅助入口，不作为新增发现机制的落点。
    /// 扫描同步执行，顺序与反射可见性依赖当前运行时和已加载程序集。
    /// </summary>
    public static class MarkerScannerExtensions
    {
        /// <summary>
        /// 扫描当前已加载程序集，可按固定名称规则排除部分系统程序集。
        /// 规则不是完整系统识别；加载、特性和回调异常沿用基础扫描器。
        /// </summary>
        /// <typeparam name="TAttr">标记特性类型。</typeparam>
        /// <param name="registry">目标注册表。</param>
        /// <param name="excludeSystemAssemblies">是否启用区分大小写的固定名称过滤，默认为真。</param>
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
        /// 深度优先收集起始程序集及引用，按程序集简单名称去重后扫描。
        /// 引用枚举和加载失败被隔离，后续实际扫描异常仍传播。
        /// </summary>
        /// <typeparam name="TAttr">标记特性类型。</typeparam>
        /// <param name="registry">目标注册表；为空仍可能先执行引用加载。</param>
        /// <param name="startAssembly">起始程序集，为空时无动作。</param>
        /// <param name="maxDepth">收集深度，零仅扫描起始程序集，负数不扫描；边界层仍尝试加载引用。</param>
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
        /// 扫描当前已加载程序集，按不区分大小写的简单名称精确排除指定项。
        /// 空排除数组不作过滤；异常及副作用沿用基础扫描器。
        /// </summary>
        /// <typeparam name="TAttr">标记特性类型。</typeparam>
        /// <param name="registry">目标注册表。</param>
        /// <param name="excludeAssemblyNames">程序集简单名称，不含扩展名。</param>
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
        /// 扫描命名空间以给定文本开头的类型，忽略大小写，不检查命名空间段边界。
        /// 此兼容入口每个类型只调用首个直接匹配特性，与基础扫描器不同。
        /// </summary>
        /// <typeparam name="TAttr">标记特性类型。</typeparam>
        /// <param name="registry">传入回调的注册表，不作空值前置检查。</param>
        /// <param name="namespacePrefix">前缀；为空字符串或空引用时不扫描。</param>
        public static void ScanByNamespace<TAttr>(this IMarkerRegistry registry, string namespacePrefix)
            where TAttr : MarkerAttribute
        {
            ScanByNamespace<TAttr>(registry, new[] { namespacePrefix });
        }

        /// <summary>
        /// 以多个前缀匹配当前已加载类型，每个类型只调用首个直接匹配特性。
        /// 忽略大小写，跳过无命名空间、抽象及接口类型；不提供去重或回滚。
        /// </summary>
        /// <typeparam name="TAttr">标记特性类型。</typeparam>
        /// <param name="registry">传入回调的注册表，不作空值前置检查。</param>
        /// <param name="namespacePrefixes">首项为空或数组为空时整次无动作；后续项不验证，空字符串可匹配任意命名空间，空引用可能抛异常。</param>
        /// <remarks>
        /// 部分类型加载失败时继续处理可用类型；其他加载、前缀匹配、特性构造及回调异常传播。
        /// 匹配按文本前缀而非命名空间层级，反射返回的特性顺序不作额外排序。
        /// </remarks>
        public static void ScanByNamespace<TAttr>(this IMarkerRegistry registry, string[] namespacePrefixes)
            where TAttr : MarkerAttribute
        {
            if (string.IsNullOrEmpty(namespacePrefixes?.FirstOrDefault()))
                return;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int a = 0; a < assemblies.Length; a++)
            {
                var asm = assemblies[a];
                if (asm == null) continue;

                Type?[] types;
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
        /// 从当前已加载程序集按简单名称的子串匹配取得新数组，忽略大小写。
        /// 不主动加载程序集，名称查询异常传播。
        /// </summary>
        /// <param name="nameContains">名称子串，空字符串匹配全部，空引用由字符串 API 抛出异常。</param>
        /// <returns>按应用域枚举顺序返回的匹配程序集数组。</returns>
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
                /*
                 * 固定名称规则仅排除部分常见系统程序集，不代表完整分类。
                 */
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
                        /*
                         * 保留引用发现的尽力加载契约，单个引用失败不阻断其他引用。
                         */
                    }
                }
            }
            catch
            {
                /*
                 * 引用枚举失败时保留已收集项，扫描阶段的异常不在此处隔离。
                 */
            }
        }
    }
}
