using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AbilityKit.Core.Common.Marker
{
    /// <summary>
    /// Marker 系统引导类，提供统一的标记扫描入口。
    /// 框架启动时调用 <see cref="ScanAll"/> 扫描所有注册的标记类型。
    /// </summary>
    /// <example>
    /// <code>
    /// // 框架初始化时调用
    /// MarkerSystem.ScanAll();
    ///
    /// // 或者扫描特定包
    /// MarkerSystem.ScanAll(new[] { typeof(MyAttribute).Assembly });
    /// </code>
    /// </example>
    public static class MarkerSystem
    {
        private static readonly List<MarkerRegistration> _registrations = new List<MarkerRegistration>();
        private static bool _initialized;

        /// <summary>
        /// 已注册的扫描项数量。
        /// </summary>
        public static int RegistrationCount => _registrations.Count;

        /// <summary>
        /// 已扫描的类型总数。
        /// </summary>
        public static int TotalScannedTypes => _registrations.Sum(r => r.Registry.Count);

        /// <summary>
        /// 注册一个标记类型及其对应的 Registry。
        /// 建议在模块的静态构造函数或初始化方法中调用。
        /// </summary>
        /// <typeparam name="TAttr">MarkerAttribute 子类</typeparam>
        /// <typeparam name="TRegistry">对应的 Registry 类型</typeparam>
        /// <param name="registry">Registry 实例</param>
        /// <param name="assemblyFilter">可选的程序集过滤器</param>
        public static void Register<TAttr, TRegistry>(TRegistry registry, Func<Assembly, bool>? assemblyFilter = null)
            where TAttr : MarkerAttribute
            where TRegistry : IMarkerRegistry
        {
            _registrations.Add(new MarkerRegistration(
                typeof(TAttr),
                registry,
                assemblyFilter
            ));
        }

        /// <summary>
        /// 注册一个标记类型及其对应的 Registry（弱类型版本）。
        /// </summary>
        public static void Register(Type attrType, IMarkerRegistry registry, Func<Assembly, bool>? assemblyFilter = null)
        {
            if (!typeof(MarkerAttribute).IsAssignableFrom(attrType))
            {
                throw new ArgumentException($"Type {attrType.FullName} must inherit from MarkerAttribute", nameof(attrType));
            }

            _registrations.Add(new MarkerRegistration(attrType, registry, assemblyFilter));
        }

        /// <summary>
        /// 扫描所有已注册的程序集中的所有标记类型。
        /// </summary>
        /// <remarks>
        /// 推荐在框架初始化时调用一次。
        /// </remarks>
        public static void ScanAll()
        {
            ScanAll(AppDomain.CurrentDomain.GetAssemblies());
        }

        /// <summary>
        /// 扫描指定程序集中的所有标记类型。
        /// </summary>
        /// <param name="assemblies">要扫描的程序集数组</param>
        public static void ScanAll(Assembly[] assemblies)
        {
            if (assemblies == null || assemblies.Length == 0)
                return;

            foreach (var registration in _registrations)
            {
                registration.Scan(assemblies);
            }

            _initialized = true;
        }

        /// <summary>
        /// 获取已注册的 Registry。
        /// </summary>
        public static TRegistry? GetRegistry<TRegistry>() where TRegistry : class, IMarkerRegistry
        {
            foreach (var registration in _registrations)
            {
                if (registration.Registry is TRegistry result)
                    return result;
            }
            return null;
        }

        /// <summary>
        /// 重置系统状态（主要用于测试）。
        /// </summary>
        public static void Reset()
        {
            _registrations.Clear();
            _initialized = false;
        }

        /// <summary>
        /// 检查是否已完成初始化扫描。
        /// </summary>
        public static bool IsInitialized => _initialized;

        /// <summary>
        /// 获取所有注册的扫描信息。
        /// </summary>
        public static IReadOnlyList<MarkerRegistration> GetRegistrations() => _registrations;
    }

    /// <summary>
    /// 单个标记类型的注册信息。
    /// </summary>
    public sealed class MarkerRegistration
    {
        public Type AttributeType { get; }
        public IMarkerRegistry Registry { get; }
        public Func<Assembly, bool>? AssemblyFilter { get; }

        private readonly MethodInfo? _genericScanMethod;

        public MarkerRegistration(Type attributeType, IMarkerRegistry registry, Func<Assembly, bool>? assemblyFilter)
        {
            AttributeType = attributeType;
            Registry = registry;
            AssemblyFilter = assemblyFilter;

            // 缓存泛型扫描方法
            var scannerType = typeof(MarkerScanner<>).MakeGenericType(attributeType);
            _genericScanMethod = scannerType.GetMethod("Scan", BindingFlags.Public | BindingFlags.Static);
        }

        /// <summary>
        /// 扫描指定程序集数组。
        /// </summary>
        public void Scan(Assembly[] allAssemblies)
        {
            if (allAssemblies == null || allAssemblies.Length == 0)
                return;

            Assembly[] assembliesToScan;
            if (AssemblyFilter != null)
            {
                assembliesToScan = FilterAssemblies(allAssemblies, AssemblyFilter);
            }
            else
            {
                assembliesToScan = allAssemblies;
            }

            if (assembliesToScan.Length == 0)
                return;

            // 调用 MarkerScanner<TAttr>.Scan(assemblies, registry)
            _genericScanMethod?.Invoke(null, new object[] { assembliesToScan, Registry });
        }

        private static Assembly[] FilterAssemblies(Assembly[] assemblies, Func<Assembly, bool> filter)
        {
            var count = 0;
            foreach (var asm in assemblies)
            {
                if (filter(asm))
                    count++;
            }
            var result = new Assembly[count];
            var index = 0;
            foreach (var asm in assemblies)
            {
                if (filter(asm))
                {
                    result[index++] = asm;
                }
            }
            return result;
        }
    }
}
