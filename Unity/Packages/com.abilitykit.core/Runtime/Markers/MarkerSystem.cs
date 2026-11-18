using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AbilityKit.Core.Markers
{
    /// <summary>
    /// 已弃用并冻结的全局标记登记与扫描入口，仅为下一主版本前兼容保留。
    /// 新发现与装配由所属所有者显式控制或通过生成式注册承接。
    /// </summary>
    /// <remarks>
    /// 锁保护登记结构和扫描状态，不使外部注册表、特性或多步启动流程线程安全。
    /// 扫描会保留部分副作用，不提供事务、回滚或通用重入屏障。
    /// </remarks>
    [Obsolete("Assembly-wide discovery does not belong to Core; migrate to an owner-controlled discovery or generated registration path before the next major version.")]
    public static class MarkerSystem
    {
        private static readonly object _syncRoot = new object();
        private static readonly object _scanRoot = new object();
        private static readonly List<MarkerRegistration> _registrations = new List<MarkerRegistration>();
        private static bool _initialized;
        private static int _version;

        /// <summary>
        /// 获取全局扫描登记项数量，不是类型数量。
        /// </summary>
        public static int RegistrationCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _registrations.Count;
                }
            }
        }

        /// <summary>
        /// 对登记快照中各注册表当前的 Count 求和，不是历史扫描或唯一类型总数。
        /// 同一注册表重复用于不同登记项时会重复计数；计数读取在登记锁外执行。
        /// </summary>
        public static int TotalScannedTypes
        {
            get
            {
                MarkerRegistration[] registrations;
                lock (_syncRoot)
                {
                    registrations = _registrations.ToArray();
                }

                return registrations.Sum(r => r.Registry.Count);
            }
        }

        /// <summary>
        /// 登记特性、注册表实例及程序集过滤器，完全相同的组合不重复登记。
        /// 新增登记使初始化标记失效；不在此执行扫描。
        /// </summary>
        /// <typeparam name="TAttr">标记特性类型。</typeparam>
        /// <typeparam name="TRegistry">注册表类型。</typeparam>
        /// <param name="registry">非空注册表实例，按引用比较。</param>
        /// <param name="assemblyFilter">可空过滤器，按委托相等比较。</param>
        public static void Register<TAttr, TRegistry>(TRegistry registry, Func<Assembly, bool>? assemblyFilter = null)
            where TAttr : MarkerAttribute
            where TRegistry : IMarkerRegistry
        {
            Register(typeof(TAttr), registry, assemblyFilter);
        }

        /// <summary>
        /// 以运行时特性类型登记扫描项，按类型、注册表引用和委托相等比较去重。
        /// 新增登记使初始化标记失效，反射扫描入口的构造异常直接传播。
        /// </summary>
        /// <param name="attrType">非空且可赋给 MarkerAttribute 的类型；不额外验证其是否闭合。</param>
        /// <param name="registry">非空注册表实例。</param>
        /// <param name="assemblyFilter">可空的程序集过滤器。</param>
        public static void Register(Type attrType, IMarkerRegistry registry, Func<Assembly, bool>? assemblyFilter = null)
        {
            if (attrType == null) throw new ArgumentNullException(nameof(attrType));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (!typeof(MarkerAttribute).IsAssignableFrom(attrType))
            {
                throw new ArgumentException($"Type {attrType.FullName} must inherit from MarkerAttribute", nameof(attrType));
            }

            lock (_syncRoot)
            {
                for (var i = 0; i < _registrations.Count; i++)
                {
                    if (_registrations[i].Matches(attrType, registry, assemblyFilter)) return;
                }

                _registrations.Add(new MarkerRegistration(attrType, registry, assemblyFilter));
                _initialized = false;
                _version++;
            }
        }

        /// <summary>
        /// 取得当前应用域已加载程序集数组并扫描当前登记快照。
        /// 每次调用均重新扫描，不因 IsInitialized 为真而跳过。
        /// </summary>
        public static void ScanAll()
        {
            ScanAll(AppDomain.CurrentDomain.GetAssemblies());
        }

        /// <summary>
        /// 按登记快照扫描指定程序集；空数组或空引用不改变状态。
        /// 非空扫描先清除初始化标记，仅在全部成功且期间版本未变化时置为已初始化。
        /// </summary>
        /// <param name="assemblies">本轮共享的程序集数组，不复制数组元素。</param>
        /// <remarks>
        /// 扫描锁在过滤及回调期间持有且允许同线程重入，登记锁不在这些回调期间持有。
        /// 过滤异常原样传播，扫描反射调用异常保留包装，失败不回滚先前登记副作用。
        /// 回调中新登记不进入外层快照；重入、登记或 Reset 会影响版本与初始化标记。
        /// </remarks>
        public static void ScanAll(Assembly[] assemblies)
        {
            if (assemblies == null || assemblies.Length == 0)
                return;

            lock (_scanRoot)
            {
                MarkerRegistration[] registrations;
                int version;
                lock (_syncRoot)
                {
                    registrations = _registrations.ToArray();
                    _initialized = false;
                    version = ++_version;
                }

                foreach (var registration in registrations)
                {
                    registration.Scan(assemblies);
                }

                lock (_syncRoot)
                {
                    if (_version == version)
                    {
                        _initialized = true;
                    }
                }
            }
        }

        /// <summary>
        /// 按登记顺序取得首个可赋给所需类型的注册表引用。
        /// </summary>
        /// <typeparam name="TRegistry">期望的注册表引用类型。</typeparam>
        /// <returns>已登记实例或空值，不复制对象。</returns>
        public static TRegistry? GetRegistry<TRegistry>() where TRegistry : class, IMarkerRegistry
        {
            lock (_syncRoot)
            {
                foreach (var registration in _registrations)
                {
                    if (registration.Registry is TRegistry result)
                        return result;
                }
            }

            return null;
        }

        /// <summary>
        /// 清除全局登记和初始化标记并改变版本，不清空或释放已有注册表。
        /// 不等待正在执行的扫描，也不撤销其后续副作用；主要用于测试隔离。
        /// </summary>
        public static void Reset()
        {
            lock (_syncRoot)
            {
                _registrations.Clear();
                _initialized = false;
                _version++;
            }
        }

        /// <summary>
        /// 获取扫描状态标记，不证明所有注册表非空、线程安全或宿主初始化完成。
        /// 回调重入时该标记还可能反映内层扫描结果。
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                lock (_syncRoot)
                {
                    return _initialized;
                }
            }
        }

        /// <summary>
        /// 取得登记项数组快照；数组结构独立，登记项及注册表引用仍共享。
        /// </summary>
        /// <returns>按登记顺序排列的浅快照，不保证深度不可变。</returns>
        public static IReadOnlyList<MarkerRegistration> GetRegistrations()
        {
            lock (_syncRoot)
            {
                return _registrations.ToArray();
            }
        }
    }

    /// <summary>
    /// 单个兼容扫描登记项，持有特性类型、注册表与过滤器引用。
    /// 可独立构造和扫描，不自行加入全局系统，也不提供并发或事务隔离。
    /// </summary>
    public sealed class MarkerRegistration
    {
        /// <summary>
        /// 获取构造时传入的特性类型。
        /// </summary>
        public Type AttributeType { get; }
        /// <summary>
        /// 获取共享的目标注册表引用。
        /// </summary>
        public IMarkerRegistry Registry { get; }
        /// <summary>
        /// 获取可空的程序集过滤器引用，执行时不隔离异常。
        /// </summary>
        public Func<Assembly, bool>? AssemblyFilter { get; }

        private readonly MethodInfo? _genericScanMethod;

        /// <summary>
        /// 保存参数并通过反射缓存闭合扫描方法，不自行登记或扫描。
        /// 不额外校验参数；类型不满足泛型条件时由反射抛出异常。
        /// </summary>
        /// <param name="attributeType">用于闭合扫描器的特性类型。</param>
        /// <param name="registry">目标注册表引用。</param>
        /// <param name="assemblyFilter">可空的程序集过滤器。</param>
        public MarkerRegistration(Type attributeType, IMarkerRegistry registry, Func<Assembly, bool>? assemblyFilter)
        {
            AttributeType = attributeType;
            Registry = registry;
            AssemblyFilter = assemblyFilter;

            /*
             * 仅缓存反射入口，不缓存扫描结果或已创建实例。
             */
            var scannerType = typeof(MarkerScanner<>).MakeGenericType(attributeType);
            _genericScanMethod = scannerType.GetMethod("Scan", BindingFlags.Public | BindingFlags.Static);
        }

        /// <summary>
        /// 空输入时无动作；否则先按数组顺序过滤，再反射调用基础扫描器。
        /// 过滤器会收到原数组中的空项；全部过滤完成后才开始扫描。
        /// </summary>
        /// <param name="allAssemblies">输入程序集数组，无过滤器时直接复用。</param>
        /// <remarks>
        /// 过滤异常原样传播；实际扫描异常通常包装为 TargetInvocationException。
        /// 过滤结果为空或未缓存到方法时不调用扫描器，不回滚已执行的回调副作用。
        /// </remarks>
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

            /*
             * 保留反射调用的异常包装，兼容入口不解包或隔离扫描异常。
             */
            _genericScanMethod?.Invoke(null, new object[] { assembliesToScan, Registry });
        }

        internal bool Matches(Type attributeType, IMarkerRegistry registry, Func<Assembly, bool>? assemblyFilter)
        {
            return AttributeType == attributeType
                && ReferenceEquals(Registry, registry)
                && Equals(AssemblyFilter, assemblyFilter);
        }

        private static Assembly[] FilterAssemblies(Assembly[] assemblies, Func<Assembly, bool> filter)
        {
            var result = new List<Assembly>(assemblies.Length);
            foreach (var asm in assemblies)
            {
                if (filter(asm))
                {
                    result.Add(asm);
                }
            }

            return result.ToArray();
        }
    }
}
