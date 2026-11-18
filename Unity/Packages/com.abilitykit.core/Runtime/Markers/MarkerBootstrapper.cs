using System;
using System.Reflection;

namespace AbilityKit.Core.Markers
{
    /// <summary>
    /// 已弃用并冻结的实例引导兼容基类，在基类构造期间可登记全局扫描项。
    /// 新代码使用所有者控制的显式或生成式注册，迁移窗口为下一主版本之前。
    /// </summary>
    /// <typeparam name="TAttr">标记特性类型。</typeparam>
    /// <typeparam name="TRegistry">注册表类型。</typeparam>
    [Obsolete("Global marker bootstrapping does not belong to Core; use owner-controlled discovery or generated registration before the next major version.")]
    public abstract class MarkerBootstrapper<TAttr, TRegistry>
        where TAttr : MarkerAttribute
        where TRegistry : class, IMarkerRegistry
    {
        /// <summary>
        /// 获取构造时传入的注册表引用，不转移所有权。
        /// </summary>
        protected TRegistry Registry { get; }

        /// <summary>
        /// 获取可空程序集过滤器；自动登记时在基类构造期间调用。
        /// 派生实例尚未完成初始化，重写属性不应依赖派生构造完成。
        /// </summary>
        protected virtual Func<Assembly, bool>? AssemblyFilter => null;

        /// <summary>
        /// 决定是否在基类实例构造期间登记全局扫描项，默认为真。
        /// 这是构造期间的虚调用，派生实例尚未完成初始化；不在此执行扫描。
        /// </summary>
        protected virtual bool AutoRegister => true;

        /// <summary>
        /// 保存非空注册表并按虚属性决定是否登记；空引用抛出 ArgumentNullException。
        /// 属性或登记失败直接传播，已执行的外部副作用不回滚。
        /// </summary>
        /// <param name="registry">非空目标注册表。</param>
        protected MarkerBootstrapper(TRegistry registry)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));

            if (AutoRegister)
            {
                MarkerSystem.Register<TAttr, TRegistry>(Registry, AssemblyFilter);
            }
        }
    }

    /// <summary>
    /// 已弃用并冻结的实例引导兼容基类，在基类构造期间可登记全局扫描项。
    /// 新代码使用所有者控制的显式或生成式注册，迁移窗口为下一主版本之前。
    /// </summary>
    /// <typeparam name="TKey">非空键类型。</typeparam>
    /// <typeparam name="TAttr">标记特性类型。</typeparam>
    /// <typeparam name="TRegistry">注册表类型。</typeparam>
    [Obsolete("Global marker bootstrapping does not belong to Core; use owner-controlled discovery or generated registration before the next major version.")]
    public abstract class KeyedMarkerBootstrapper<TKey, TAttr, TRegistry>
        where TKey : notnull
        where TAttr : MarkerAttribute
        where TRegistry : KeyedMarkerRegistry<TKey, TAttr>
    {
        /// <summary>
        /// 获取构造时传入的注册表引用，不转移所有权。
        /// </summary>
        protected TRegistry Registry { get; }

        /// <summary>
        /// 获取可空程序集过滤器；自动登记时在基类构造期间调用。
        /// 派生实例尚未完成初始化，重写属性不应依赖派生构造完成。
        /// </summary>
        protected virtual Func<Assembly, bool>? AssemblyFilter => null;

        /// <summary>
        /// 决定是否在基类实例构造期间登记全局扫描项，默认为真。
        /// 这是构造期间的虚调用，派生实例尚未完成初始化；不在此执行扫描。
        /// </summary>
        protected virtual bool AutoRegister => true;

        /// <summary>
        /// 保存非空注册表并按虚属性决定是否登记；空引用抛出 ArgumentNullException。
        /// 属性或登记失败直接传播，已执行的外部副作用不回滚。
        /// </summary>
        /// <param name="registry">非空目标注册表。</param>
        protected KeyedMarkerBootstrapper(TRegistry registry)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));

            if (AutoRegister)
            {
                MarkerSystem.Register<TAttr, TRegistry>(Registry, AssemblyFilter);
            }
        }
    }

    /// <summary>
    /// 已弃用并冻结的静态初始化兼容基类，通过每个封闭泛型的静态构造执行一次注册。
    /// 不会因模块文件加载或取得 typeof 就保证执行，新代码改用显式所有者注册。
    /// </summary>
    /// <typeparam name="TSelf">具有公开无参构造的派生类型。</typeparam>
    /// <typeparam name="TAttr">标记特性类型。</typeparam>
    /// <typeparam name="TRegistry">注册表引用类型。</typeparam>
    /// <remarks>
    /// 静态构造会创建 TSelf 并调用 Register；失败沿用运行时静态初始化失败语义，不回滚部分副作用。
    /// 下一主版本前保留兼容，不新增消费者或自动启动机制。
    /// </remarks>
    [Obsolete("Static registration side effects do not belong to Core; use explicit owner-controlled or generated registration before the next major version.")]
    public abstract class StaticMarkerBootstrapper<TSelf, TAttr, TRegistry>
        where TSelf : StaticMarkerBootstrapper<TSelf, TAttr, TRegistry>, new()
        where TAttr : MarkerAttribute
        where TRegistry : class, IMarkerRegistry
    {
        static StaticMarkerBootstrapper()
        {
            var self = new TSelf();
            self.Register();
        }

        /// <summary>
        /// 提供供默认 Register 使用的注册表，不规定是否新建或复用实例。
        /// </summary>
        /// <returns>交给全局登记入口的注册表引用。</returns>
        protected abstract TRegistry CreateRegistry();

        /// <summary>
        /// 提供默认 Register 使用的可空程序集过滤器。
        /// </summary>
        protected virtual Func<Assembly, bool>? AssemblyFilter => null;

        /// <summary>
        /// 默认通过 CreateRegistry 和 AssemblyFilter 向全局系统登记，不执行扫描。
        /// 重写可以改变副作用；执行于封闭泛型的静态构造期间，异常不隔离。
        /// </summary>
        protected virtual void Register()
        {
            MarkerSystem.Register<TAttr, TRegistry>(CreateRegistry(), AssemblyFilter);
        }

        /// <summary>
        /// 调用时保证声明类型的静态构造已完成，不重复执行注册。
        /// MarkerSystem.Reset 后再次调用不会重新登记；初始化失败按运行时规则传播。
        /// </summary>
        public static void EnsureInitialized()
        {
            /*
             * 调用此静态方法会先完成声明类型的静态构造；typeof 表达式本身不负责创建实例。
             */
            _ = typeof(TSelf);
        }
    }
}
